using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Storage.Abstractions;
using SamoBot.Infrastructure.Utilities;

namespace SamoBot.Infrastructure.Storage.Services;

public class ContentProcessingPipeline : IContentProcessingPipeline
{
    private readonly IUrlFetchService _fetchService;
    private readonly IHtmlContentValidator _htmlContentValidator;
    private readonly IMinioHtmlUploader _htmlUploader;
    private readonly IFetchRecordPersistenceService _persistenceService;
    private readonly IObjectNameGenerator _objectNameGenerator;
    private readonly MinioOptions _minioOptions;
    private readonly ILogger<ContentProcessingPipeline> _logger;
    private readonly ICache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly InstanceIdProvider _instanceIdProvider;
    private readonly CrawlerOptions _crawlerOptions;

    public ContentProcessingPipeline(
        IUrlFetchService fetchService,
        IHtmlContentValidator htmlContentValidator,
        IMinioHtmlUploader htmlUploader,
        IFetchRecordPersistenceService persistenceService,
        IObjectNameGenerator objectNameGenerator,
        IOptions<MinioOptions> minioOptions,
        ILogger<ContentProcessingPipeline> logger, 
        ICache cache,
        TimeProvider timeProvider,
        InstanceIdProvider instanceIdProvider,
        IOptions<CrawlerOptions> crawlerOptions)
    {
        _fetchService = fetchService;
        _htmlContentValidator = htmlContentValidator;
        _htmlUploader = htmlUploader;
        _persistenceService = persistenceService;
        _objectNameGenerator = objectNameGenerator;
        _minioOptions = minioOptions.Value;
        _logger = logger;
        _cache = cache;
        _timeProvider = timeProvider;
        _instanceIdProvider = instanceIdProvider;
        _crawlerOptions = crawlerOptions.Value;
    }

    public async Task<Result<UrlContentMetadata>> ProcessContent(ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;
        var host = scheduledUrl.Host;
        var now = _timeProvider.GetUtcNow();
        var currentTimestamp = now.ToUnixTimeMilliseconds();
        const long oneMinuteInMs = 60 * TimeSpan.MillisecondsPerSecond;
        
        // Attempt to acquire lock with instance ID for ownership tracking
        var lockKey = CacheKey.UrlLock(host);
        var lockValue = $"{_instanceIdProvider.InstanceId}:{currentTimestamp}";
        var lockTtl = TimeSpan.FromSeconds(_crawlerOptions.UrlLockTtlSeconds);
        
        var lockResult = await _cache.TrySetAsync(lockKey, lockValue, lockTtl, cancellationToken);

        if (lockResult.IsFailed)
        {
            _logger.LogWarning("Failed to attempt lock acquisition for host {Host}: {Errors}", 
                host, string.Join("; ", lockResult.Errors.Select(e => e.Message)));
            return Result.Fail<UrlContentMetadata>(lockResult.Errors);
        }

        // Check if lock was acquired
        if (!lockResult.Value)
        {
            // Lock already exists - another instance is handling this host
            _logger.LogInformation("Lock already held for host {Host}, skipping URL {Url}", host, url);
            return Result.Ok(new UrlContentMetadata
            {
                ContentType = string.Empty,
                ContentLength = -1,
                StatusCode = 0
            });
        }

        // Lock acquired - now check UrlNextCrawl while holding the lock to prevent race conditions
        try
        {
            var lastCrawlResult = await _cache.GetAsync(CacheKey.UrlNextCrawl(host), cancellationToken);
            if (lastCrawlResult.IsSuccess && lastCrawlResult.Value != null)
            {
                if (long.TryParse(lastCrawlResult.Value, out var lastCrawlTimestamp))
                {
                    var timeSinceLastCrawl = currentTimestamp - lastCrawlTimestamp;
                    
                    if (timeSinceLastCrawl < oneMinuteInMs)
                    {
                        // Host was crawled less than 1 minute ago, enqueue for later
                        var dueTimestamp = lastCrawlTimestamp + oneMinuteInMs;
                        var enqueueResult = await _cache.EnqueueDueAsync(url, dueTimestamp, cancellationToken);
                        
                        if (enqueueResult.IsFailed)
                        {
                            _logger.LogWarning("Failed to enqueue URL {Url} to due queue: {Errors}", 
                                url, string.Join("; ", enqueueResult.Errors.Select(e => e.Message)));
                            return Result.Fail<UrlContentMetadata>(enqueueResult.Errors);
                        }
                        
                        _logger.LogInformation("Enqueued URL {Url} for host {Host} due at {DueTime}", 
                            url, host, DateTimeOffset.FromUnixTimeMilliseconds(dueTimestamp));
                        
                        return Result.Ok(new UrlContentMetadata
                        {
                            ContentType = string.Empty,
                            ContentLength = -1,
                            StatusCode = 0
                        });
                    }
                }
            }

            // Proceed with fetch - we hold the lock and have verified it's been at least 1 minute
            _logger.LogInformation("Acquired lock for host {Host}, processing URL {Url}", host, url);
            
            var fetchResult = await _fetchService.Fetch(url, cancellationToken);
            _logger.LogInformation("PROCESSING URL {Url} for host {Host}", url, host);
            
            // Set the next crawl timestamp BEFORE releasing the lock to ensure it's visible
            await _cache.SetAsync(CacheKey.UrlNextCrawl(host), currentTimestamp.ToString(), cancellationToken: cancellationToken);
            
            // Small delay to ensure the timestamp is propagated in Redis before releasing lock
            await Task.Delay(50, cancellationToken);

            return Result.Ok(new UrlContentMetadata
            {
                ContentType = fetchResult.ContentType ?? string.Empty,
                ContentLength = fetchResult.ContentLength ?? -1,
                StatusCode = fetchResult.StatusCode
            });
        }
        finally
        {
            // Verify ownership before releasing lock to prevent releasing another instance's lock
            var currentLockValue = await _cache.GetAsync(lockKey, cancellationToken);
            if (currentLockValue.IsSuccess && currentLockValue.Value == lockValue)
            {
                // We own the lock, safe to release
                await _cache.RemoveAsync(lockKey, cancellationToken);
                _logger.LogDebug("Released lock for host {Host}", host);
            }
            else
            {
                // Lock value doesn't match - either expired, was released, or belongs to another instance
                _logger.LogWarning("Lock value mismatch for host {Host}. Expected: {Expected}, Got: {Actual}. Lock may have expired or been released by another instance.", 
                    host, lockValue, currentLockValue.Value ?? "null");
            }
        }
    }
}

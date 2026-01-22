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

    public ContentProcessingPipeline(
        IUrlFetchService fetchService,
        IHtmlContentValidator htmlContentValidator,
        IMinioHtmlUploader htmlUploader,
        IFetchRecordPersistenceService persistenceService,
        IObjectNameGenerator objectNameGenerator,
        IOptions<MinioOptions> minioOptions,
        ILogger<ContentProcessingPipeline> logger, 
        ICache cache,
        TimeProvider timeProvider)
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
    }

    public async Task<Result<UrlContentMetadata>> ProcessContent(ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;
        var host = scheduledUrl.Host;
        var now = _timeProvider.GetUtcNow();
        var currentTimestamp = now.ToUnixTimeMilliseconds();
        
        var lastCrawlResult = await _cache.GetAsync(CacheKey.UrlNextCrawl(host), cancellationToken);
        if (lastCrawlResult.IsSuccess && lastCrawlResult.Value != null)
        {
            if (long.TryParse(lastCrawlResult.Value, out var lastCrawlTimestamp))
            {
                var timeSinceLastCrawl = currentTimestamp - lastCrawlTimestamp;
                const long oneMinuteInMs = TimeSpan.MillisecondsPerSecond;
                
                if (timeSinceLastCrawl < oneMinuteInMs)
                {
                    // Host was crawled less than 1 minute ago, enqueue for later
                    var dueTimestamp = lastCrawlTimestamp + oneMinuteInMs;
                    var enqueueResult = await _cache.EnqueueDueAsync(url.ToString(), dueTimestamp, cancellationToken);
                    
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
        
        var lockResult = await _cache.SetAsync(CacheKey.UrlLock(host), currentTimestamp.ToString(), TimeSpan.FromSeconds(2), cancellationToken);

        if (lockResult.IsFailed)
        {
            return Result.Fail<UrlContentMetadata>(lockResult.Errors);
        }
        
        var fetchResult = await _fetchService.Fetch(url.ToString(), cancellationToken);
        _logger.LogInformation("PROCESSING URL {Url} for host {Host}", 
            url, host);
        await _cache.RemoveAsync(CacheKey.UrlLock(host), cancellationToken);
        
        await _cache.SetAsync(CacheKey.UrlNextCrawl(host), currentTimestamp.ToString(), cancellationToken: cancellationToken);

        return Result.Ok(new UrlContentMetadata
        {
            ContentType = fetchResult.ContentType ?? string.Empty,
            ContentLength = fetchResult.ContentLength ?? -1,
            StatusCode = fetchResult.StatusCode
        });
    }
}

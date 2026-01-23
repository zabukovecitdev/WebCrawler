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

        var claimResult = await _cache.TryClaimNextCrawlAsync(
            CacheKey.UrlNextCrawl(host),
            currentTimestamp,
            oneMinuteInMs,
            cancellationToken);

        if (claimResult.IsFailed)
        {
            _logger.LogWarning("Failed to claim next crawl slot for host {Host}: {Errors}",
                host, string.Join("; ", claimResult.Errors.Select(e => e.Message)));
            return Result.Fail<UrlContentMetadata>(claimResult.Errors);
        }

        if (!claimResult.Value.Allowed)
        {
            var dueTimestamp = claimResult.Value.NextAllowedTimestamp;
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
                StatusCode = 0,
                WasDeferred = true
            });
        }

        _logger.LogInformation("Claimed crawl slot for host {Host}, processing URL {Url}", host, url);

        var fetchResult = await _fetchService.Fetch(url, cancellationToken);
        _logger.LogInformation("PROCESSING URL {Url} for host {Host}", url, host);

        return Result.Ok(new UrlContentMetadata
        {
            ContentType = fetchResult.ContentType ?? string.Empty,
            ContentLength = fetchResult.ContentLength ?? -1,
            StatusCode = fetchResult.StatusCode,
            WasDeferred = false
        });
    }
}

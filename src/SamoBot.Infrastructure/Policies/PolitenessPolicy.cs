using System.Text.Json;
using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Utilities;

namespace SamoBot.Infrastructure.Policies;

public class PolitenessPolicy : ICrawlPolicy
{
    private readonly ICache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly CrawlerOptions _crawlerOptions;
    private readonly ILogger<PolitenessPolicy> _logger;

    public PolitenessPolicy(ICache cache, TimeProvider timeProvider, IOptions<CrawlerOptions> crawlerOptions,
        ILogger<PolitenessPolicy> logger)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _crawlerOptions = crawlerOptions.Value;
        _logger = logger;
    }

    public async Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;
        var host = scheduledUrl.Host;
        var now = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var delayMs = GetPerHostDelayMs();

        var claimResult = await _cache.TryClaimNextCrawl(
            CacheKey.UrlNextCrawl(host),
            now,
            delayMs,
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
            var payload = JsonSerializer.Serialize(scheduledUrl);
            var enqueueResult = await _cache.EnqueueUrlForCrawl(payload, dueTimestamp, cancellationToken);

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
                WasDeferred = true
            });
        }

        _logger.LogInformation("Claimed crawl slot for host {Host}, executing policy for URL {Url}", host, url);
        
        return await action(cancellationToken);
    }

    private long GetPerHostDelayMs()
    {
        var delayMs = _crawlerOptions.DefaultDelayMs;
        if (delayMs < _crawlerOptions.MinDelayMs)
        {
            delayMs = _crawlerOptions.MinDelayMs;
        }

        if (delayMs > _crawlerOptions.MaxDelayMs)
        {
            delayMs = _crawlerOptions.MaxDelayMs;
        }

        return Math.Max(0, delayMs);
    }
}

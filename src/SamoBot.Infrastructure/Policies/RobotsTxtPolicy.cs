using FluentResults;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Services;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Infrastructure.Policies;

public class RobotsTxtPolicy : ICrawlPolicy
{
    private readonly IRobotsTxtService _robotsTxtService;
    private readonly ICrawlTelemetryService _telemetry;
    private readonly ILogger<RobotsTxtPolicy> _logger;
    private const string UserAgent = "SamoBot";

    public RobotsTxtPolicy(
        IRobotsTxtService robotsTxtService,
        ICrawlTelemetryService telemetry,
        ILogger<RobotsTxtPolicy> logger)
    {
        _robotsTxtService = robotsTxtService;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;

        if (!scheduledUrl.RespectRobots)
        {
            _logger.LogDebug("Skipping robots.txt check for {Url} (RespectRobots=false)", url);
            return await action(cancellationToken);
        }

        var isAllowedResult = await _robotsTxtService.IsUrlAllowed(url, UserAgent, cancellationToken);

        if (isAllowedResult.IsFailed)
        {
            _logger.LogWarning("Failed to check robots.txt for {Url}, allowing crawl: {Errors}",
                url, string.Join("; ", isAllowedResult.Errors.Select(e => e.Message)));
            return await action(cancellationToken);
        }

        if (!isAllowedResult.Value)
        {
            _logger.LogInformation("URL {Url} blocked by robots.txt", url);
            await _telemetry.PublishAsync(scheduledUrl.CrawlJobId, "BlockedByRobots",
                new { url, discoveredUrlId = scheduledUrl.Id }, cancellationToken);
            return Result.Ok(new UrlContentMetadata
            {
                WasBlocked = true
            });
        }

        return await action(cancellationToken);
    }
}

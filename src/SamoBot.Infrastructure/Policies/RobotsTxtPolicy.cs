using FluentResults;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Services;

namespace SamoBot.Infrastructure.Policies;

public class RobotsTxtPolicy : ICrawlPolicy
{
    private readonly IRobotsTxtService _robotsTxtService;
    private readonly ILogger<RobotsTxtPolicy> _logger;
    private const string UserAgent = "SamoBot";

    public RobotsTxtPolicy(IRobotsTxtService robotsTxtService, ILogger<RobotsTxtPolicy> logger)
    {
        _robotsTxtService = robotsTxtService;
        _logger = logger;
    }

    public async Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;

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
            return Result.Ok(new UrlContentMetadata
            {
                WasBlocked = true
            });
        }

        return await action(cancellationToken);
    }
}

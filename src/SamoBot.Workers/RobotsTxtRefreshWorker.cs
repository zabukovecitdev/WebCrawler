using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Services;

namespace SamoBot.Workers;

/// <summary>
/// Background worker that refreshes expiring robots.txt entries to keep cache warm.
/// </summary>
public class RobotsTxtRefreshWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RobotsTxtRefreshWorker> _logger;
    private const int CheckIntervalMinutes = 60;
    private const int ExpiresWithinHours = 4;
    private const int BatchLimit = 100;

    public RobotsTxtRefreshWorker(
        IServiceProvider serviceProvider,
        ILogger<RobotsTxtRefreshWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RobotsTxt refresh worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringRobotsTxt(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in RobotsTxtRefreshWorker");
            }

            // Wait before next iteration
            await Task.Delay(TimeSpan.FromMinutes(CheckIntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("RobotsTxt refresh worker stopping...");
    }

    private async Task RefreshExpiringRobotsTxt(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRobotsTxtRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IRobotsTxtService>();

        try
        {
            var expiring = await repository.GetExpiringAsync(
                TimeSpan.FromHours(ExpiresWithinHours),
                BatchLimit,
                cancellationToken);

            if (expiring.Count == 0)
            {
                _logger.LogDebug("No expiring robots.txt entries found");
                return;
            }

            _logger.LogInformation("Found {Count} expiring robots.txt entries, refreshing", expiring.Count);

            var successCount = 0;
            var failCount = 0;

            foreach (var robotsTxt in expiring)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var result = await service.GetRobotsTxt(robotsTxt.Host, cancellationToken);

                    if (result.IsSuccess)
                    {
                        successCount++;
                        _logger.LogDebug("Successfully refreshed robots.txt for host {Host}", robotsTxt.Host);
                    }
                    else
                    {
                        failCount++;
                        _logger.LogWarning("Failed to refresh robots.txt for host {Host}: {Errors}",
                            robotsTxt.Host, string.Join("; ", result.Errors.Select(e => e.Message)));
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _logger.LogWarning(ex, "Exception while refreshing robots.txt for host {Host}", robotsTxt.Host);
                }

                // Small delay between refreshes to avoid hammering servers
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }

            _logger.LogInformation(
                "Completed robots.txt refresh: {SuccessCount} successful, {FailCount} failed",
                successCount, failCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expiring robots.txt entries");
        }
    }
}

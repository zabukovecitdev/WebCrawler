using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Data.Abstractions;

namespace SamoBot.Workers;

/// <summary>
/// Background worker that resets orphaned InFlight URLs back to Idle when they have been stuck
/// (e.g. worker died before calling UpdateAfterFetch). Sets NextCrawlAt so they are retried soon.
/// </summary>
public class OrphanedInFlightWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrphanedInFlightWorker> _logger;

    private static readonly TimeSpan RunInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InFlightStuckThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan NextCrawlDelay = TimeSpan.FromMinutes(5);
    private const int BatchLimit = 500;

    public OrphanedInFlightWorker(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<OrphanedInFlightWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Orphaned InFlight worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetStuckInFlightUrls(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrphanedInFlightWorker");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("Orphaned InFlight worker stopped.");
    }

    private async Task ResetStuckInFlightUrls(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDiscoveredUrlRepository>();

        var stuckIds = (await repository.GetStuckInFlightIds(InFlightStuckThreshold, BatchLimit, cancellationToken))
            .ToList();

        if (stuckIds.Count == 0)
        {
            _logger.LogDebug("No orphaned InFlight URLs found");
            return;
        }

        var nextCrawlAt = _timeProvider.GetUtcNow().Add(NextCrawlDelay);
        var resetCount = await repository.ResetOrphanedInFlightToIdle(stuckIds, nextCrawlAt, cancellationToken);

        _logger.LogInformation(
            "Reset {ResetCount} orphaned InFlight URL(s) to Idle with NextCrawlAt {NextCrawlAt}",
            resetCount, nextCrawlAt);
    }
}

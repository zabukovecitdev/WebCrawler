using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Abstractions;

namespace SamoBot.Workers;

public class SchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SchedulerWorker> _logger;

    public SchedulerWorker(
        IServiceProvider serviceProvider,
        TimeProvider timeProvider,
        ILogger<SchedulerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scheduler worker started.");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scheduler worker running at: {Time}", _timeProvider.GetUtcNow());

                await ProcessUrlsReadyForCrawling(cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Scheduler worker cancellation requested.");
        }
        finally
        {
            _logger.LogInformation("Scheduler worker stopped.");
        }
    }

    private async Task ProcessUrlsReadyForCrawling(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var schedulerService = scope.ServiceProvider.GetRequiredService<ISchedulerService>();
        var urlProducer = scope.ServiceProvider.GetRequiredService<IUrlScheduler>();

        try
        {
            var urls = await schedulerService.GetScheduledEntities(10, cancellationToken);

            var urlList = urls.ToList();

            if (urlList.Count > 0)
            {
                _logger.LogInformation("Found {Count} URLs ready for crawling", urlList.Count);

                await urlProducer.PublishBatch(urlList, cancellationToken);

                _logger.LogInformation("Published {Count} URLs to scheduled URL queue", urlList.Count);

                foreach (var url in urlList)
                {
                    _logger.LogDebug("Published URL for crawling: {Url} (ID: {Id}, Priority: {Priority})",
                        url.Url, url.Id, url.Priority);
                }
            }
            else
            {
                _logger.LogDebug("No URLs ready for crawling at this time");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing URLs ready for crawling");
        }
    }
}

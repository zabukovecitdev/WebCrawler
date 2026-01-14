using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SamoBot.Scheduler.Workers;

public class SchedulerWorker : BackgroundService
{
    private readonly ILogger<SchedulerWorker> _logger;

    public SchedulerWorker(ILogger<SchedulerWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scheduler worker running at: {Time}", DateTimeOffset.Now);
                
                // TODO: Add scheduling logic here
                
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Scheduler worker cancellation requested.");
        }
        finally
        {
            _logger.LogInformation("Scheduler worker stopped.");
        }
    }
}

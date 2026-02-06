using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Abstractions;

namespace SamoBot.Workers;

public class ParserWorker : BackgroundService
{
    private readonly ILogger<ParserWorker> _logger;
    private readonly IParserService _parserService;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);

    public ParserWorker(
        ILogger<ParserWorker> logger,
        IParserService parserService)
    {
        _logger = logger;
        _parserService = parserService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parser worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _parserService.ProcessUnparsedFetches(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in parser worker execution cycle");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("Parser worker stopping...");
    }
}

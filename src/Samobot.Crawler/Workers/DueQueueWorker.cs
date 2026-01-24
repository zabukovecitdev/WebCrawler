using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace Samobot.Crawler.Workers;

/// <summary>
/// Worker that processes URLs from the due queue that are ready to be crawled
/// </summary>
public class DueQueueWorker : BackgroundService
{
    private readonly ILogger<DueQueueWorker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private const int PollIntervalSeconds = 5; // Check every 5 seconds
    private const int BatchSize = 50; // Process up to 50 URLs per iteration

    public DueQueueWorker(
        ILogger<DueQueueWorker> logger,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Due queue worker starting...");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueQueueAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing due queue");
            }

            // Wait before next iteration
            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), cancellationToken);
        }

        _logger.LogInformation("Due queue worker stopping...");
    }

    private async Task ProcessDueQueueAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICache>();
        var contentPipeline = scope.ServiceProvider.GetRequiredService<IContentProcessingPipeline>();

        var currentTimestamp = _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
        var dequeueResult = await cache.Dequeue(currentTimestamp, BatchSize, cancellationToken);

        if (dequeueResult.IsFailed)
        {
            _logger.LogWarning("Failed to dequeue from due queue: {Errors}",
                string.Join("; ", dequeueResult.Errors.Select(e => e.Message)));
            return;
        }

        var urls = dequeueResult.Value;
        if (urls.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Processing {Count} URLs from due queue", urls.Count);

        foreach (var payload in urls)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                ScheduledUrl? scheduledUrl = null;

                try
                {
                    scheduledUrl = JsonSerializer.Deserialize<ScheduledUrl>(payload);
                }
                catch (JsonException)
                {
                    // Fallback for legacy payloads that only contained the URL string.
                }

                if (scheduledUrl == null || string.IsNullOrWhiteSpace(scheduledUrl.Url))
                {
                    if (!Uri.TryCreate(payload, UriKind.Absolute, out var uri))
                    {
                        _logger.LogWarning("Invalid due queue payload: {Payload}", payload);
                        continue;
                    }

                    scheduledUrl = new ScheduledUrl
                    {
                        Host = uri.Host,
                        Url = uri.ToString()
                    };
                }
                else if (string.IsNullOrWhiteSpace(scheduledUrl.Host) &&
                         Uri.TryCreate(scheduledUrl.Url, UriKind.Absolute, out var uri))
                {
                    scheduledUrl.Host = uri.Host;
                }

                var result = await contentPipeline.ProcessContent(scheduledUrl, cancellationToken);

                if (result.IsFailed)
                {
                    _logger.LogWarning("Failed to process URL {Url} from due queue: {Errors}",
                        scheduledUrl.Url, string.Join("; ", result.Errors.Select(e => e.Message)));
                }
                else if (result.Value.WasDeferred)
                {
                    _logger.LogInformation("Deferred URL {Url} again; still not due", scheduledUrl.Url);
                }
                else
                {
                    _logger.LogDebug("Successfully processed URL {Url} from due queue", scheduledUrl.Url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing due queue payload {Payload}", payload);
            }
        }
    }
}

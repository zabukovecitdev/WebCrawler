using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using SamoBot.Api.Hubs;
using SamoBot.Infrastructure.Options;
using StackExchange.Redis;

namespace SamoBot.Api.Hosting;

public class CrawlTelemetryRedisSubscriber : BackgroundService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IHubContext<CrawlJobHub> _hubContext;
    private readonly CrawlTelemetryOptions _options;
    private readonly ILogger<CrawlTelemetryRedisSubscriber> _logger;

    public CrawlTelemetryRedisSubscriber(
        IConnectionMultiplexer? redis,
        IHubContext<CrawlJobHub> hubContext,
        IOptions<CrawlTelemetryOptions> options,
        ILogger<CrawlTelemetryRedisSubscriber> logger)
    {
        _redis = redis;
        _hubContext = hubContext;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_redis is null || !_redis.IsConnected)
        {
            _logger.LogWarning("Redis not available; crawl telemetry will only be available via REST polling.");
            return;
        }

        var sub = _redis.GetSubscriber();
        sub.Subscribe(
            RedisChannel.Literal(_options.RedisChannel),
            (channel, value) =>
            {
                _ = ForwardToSignalRAsync(value, stoppingToken);
            });

        _logger.LogInformation("Subscribed to crawl telemetry channel {Channel}", _options.RedisChannel);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        finally
        {
            sub.Unsubscribe(RedisChannel.Literal(_options.RedisChannel));
        }
    }

    private async Task ForwardToSignalRAsync(RedisValue message, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(message.ToString());
            var root = doc.RootElement;
            if (!root.TryGetProperty("crawlJobId", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
            {
                return;
            }

            var jobId = idEl.GetInt32();
            await _hubContext.Clients.Group(CrawlJobHub.JobGroupName(jobId))
                .SendAsync("CrawlEvent", root, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to forward crawl telemetry to SignalR");
        }
    }
}

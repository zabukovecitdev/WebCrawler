using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services.Abstractions;
using StackExchange.Redis;

namespace SamoBot.Infrastructure.Services;

public class CrawlTelemetryService : ICrawlTelemetryService
{
    private readonly ICrawlJobEventRepository _events;
    private readonly IConnectionMultiplexer? _redis;
    private readonly CrawlTelemetryOptions _options;
    private readonly ILogger<CrawlTelemetryService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public CrawlTelemetryService(
        ICrawlJobEventRepository events,
        IConnectionMultiplexer? redis,
        IOptions<CrawlTelemetryOptions> options,
        ILogger<CrawlTelemetryService> logger)
    {
        _events = events;
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(int? crawlJobId, string eventType, object payload, CancellationToken cancellationToken = default)
    {
        if (crawlJobId is null or <= 0)
        {
            return;
        }

        var jobId = crawlJobId.Value;
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        long eventId;
        try
        {
            eventId = await _events.Append(jobId, eventType, payloadJson, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist crawl telemetry for job {JobId}", jobId);
            return;
        }

        if (_redis is null || !_redis.IsConnected)
        {
            return;
        }

        try
        {
            var envelope = JsonSerializer.Serialize(new
            {
                crawlJobId = jobId,
                eventId,
                eventType,
                payload = JsonSerializer.Deserialize<JsonElement>(payloadJson)
            }, JsonOptions);

            await _redis.GetSubscriber().PublishAsync(
                RedisChannel.Literal(_options.RedisChannel),
                envelope);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Redis publish failed for crawl telemetry (job {JobId})", jobId);
        }
    }
}

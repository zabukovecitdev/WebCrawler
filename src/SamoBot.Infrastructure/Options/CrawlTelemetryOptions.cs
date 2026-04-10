namespace SamoBot.Infrastructure.Options;

public class CrawlTelemetryOptions
{
    public const string SectionName = "CrawlTelemetry";

    /// <summary>Redis Pub/Sub channel for forwarding events to the API SignalR layer.</summary>
    public string RedisChannel { get; set; } = "samo:crawl:telemetry";
}

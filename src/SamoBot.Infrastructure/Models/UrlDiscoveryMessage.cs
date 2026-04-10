namespace SamoBot.Infrastructure.Models;

/// <summary>
/// Serialized to RabbitMQ for discovered URLs (JSON). Plain string messages are treated as URL-only legacy payloads.
/// </summary>
public class UrlDiscoveryMessage
{
    public string Url { get; set; } = string.Empty;
    public int? CrawlJobId { get; set; }
    public int Depth { get; set; }
    public bool UseJsRendering { get; set; }
    public bool RespectRobots { get; set; } = true;
}

namespace Samobot.Domain.Models;

public class UrlFetch
{
    public int Id { get; set; }
    public int DiscoveredUrlId { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public int StatusCode { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public long? ResponseTimeMs { get; set; }
    public string? ObjectName { get; set; }
    public DiscoveredUrl? DiscoveredUrl { get; set; }
}

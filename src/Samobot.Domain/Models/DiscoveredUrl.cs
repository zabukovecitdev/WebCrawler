using Samobot.Domain.Enums;

namespace Samobot.Domain.Models;

public class DiscoveredUrl
{
    public int Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? NormalizedUrl { get; set; }
    public UrlStatus Status { get; set; } = UrlStatus.None;
    public DateTimeOffset? LastCrawlAt { get; set; }
    public DateTimeOffset? NextCrawlAt { get; set; }
    public uint FailCount { get; set; }
    public int LastStatusCode { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public int Priority { get; set; }
    public string? ContentType { get; set; }
    public long? ContentLength { get; set; }
    public string? ObjectName { get; set; }
}

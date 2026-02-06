using Samobot.Infrastructure.Enums;

namespace SamoBot.Infrastructure.Models;

public class DiscoveredUrl
{
    public int Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? NormalizedUrl { get; set; }
    public UrlStatus Status { get; set; } = UrlStatus.None;
    public DateTimeOffset? LastCrawlAt { get; set; }
    public DateTimeOffset? NextCrawlAt { get; set; }
    public int FailCount { get; set; }
    public DateTimeOffset DiscoveredAt { get; set; }
    public int Priority { get; set; }
    public int? LastFetchId { get; set; }
}

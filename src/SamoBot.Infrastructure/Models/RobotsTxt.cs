namespace SamoBot.Infrastructure.Models;

public class RobotsTxt
{
    public int Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<RobotsTxtRule> Rules { get; set; } = new();
    public int? CrawlDelayMs { get; set; }
    public List<string> SitemapUrls { get; set; } = new();
    public DateTime FetchedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsFetchError { get; set; }
    public string? ErrorMessage { get; set; }
    public int? StatusCode { get; set; }
}

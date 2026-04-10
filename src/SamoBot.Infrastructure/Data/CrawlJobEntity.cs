using SamoBot.Infrastructure.Enums;

namespace SamoBot.Infrastructure.Data;

public class CrawlJobEntity
{
    public int Id { get; set; }
    public string? OwnerUserId { get; set; }
    public string Status { get; set; } = nameof(CrawlJobStatus.Draft);
    public string SeedUrls { get; set; } = "[]";
    public int? MaxDepth { get; set; }
    public int? MaxUrls { get; set; }
    public bool UseJsRendering { get; set; }
    public bool RespectRobots { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public CrawlJobStatus GetStatus() =>
        Enum.TryParse<CrawlJobStatus>(Status, out var s) ? s : CrawlJobStatus.Draft;
}

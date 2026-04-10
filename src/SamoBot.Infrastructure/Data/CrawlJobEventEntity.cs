namespace SamoBot.Infrastructure.Data;

public class CrawlJobEventEntity
{
    public long Id { get; set; }
    public int CrawlJobId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

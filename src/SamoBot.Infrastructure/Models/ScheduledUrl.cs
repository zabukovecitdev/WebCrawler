namespace SamoBot.Infrastructure.Models;

public class ScheduledUrl
{
    public int Id { get; set; }
    public string Host { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int? CrawlJobId { get; set; }
    public int Depth { get; set; }
    public bool UseJsRendering { get; set; }
    public bool RespectRobots { get; set; } = true;
}

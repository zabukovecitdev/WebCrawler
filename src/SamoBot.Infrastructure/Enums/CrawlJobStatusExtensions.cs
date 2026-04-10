namespace SamoBot.Infrastructure.Enums;

public static class CrawlJobStatusExtensions
{
    public static string AsString(this CrawlJobStatus status) => status.ToString();
}

namespace SamoBot.Infrastructure.Utilities;

public static class CacheKey
{
    public static string UrlNextCrawl(string host) => $"host:next_crawl:{host}";
    public static string DueQueue() => "urls:due:queue";
}
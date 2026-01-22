namespace SamoBot.Infrastructure.Utilities;

public static class CacheKey
{
    public static string UrlLock(string host) => $"host:lock:{host}";
    public static string UrlNextCrawl(string host) => $"host:next_crawl:{host}";
    public static string DueQueue() => "urls:due:queue";
}
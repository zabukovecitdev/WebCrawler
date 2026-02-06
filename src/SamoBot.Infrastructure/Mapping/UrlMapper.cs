using Riok.Mapperly.Abstractions;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Mapping;

[Mapper]
public static partial class UrlMapper
{
    [MapProperty(nameof(DiscoveredUrl.Id), nameof(ScheduledUrl.Id))]
    [MapProperty(nameof(DiscoveredUrl.Priority), nameof(ScheduledUrl.Priority))]
    [MapperIgnoreTarget(nameof(ScheduledUrl.Url))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.Url))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.NormalizedUrl))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.Status))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.LastCrawlAt))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.NextCrawlAt))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.FailCount))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.DiscoveredAt))]
    [MapperIgnoreSource(nameof(DiscoveredUrl.LastFetchId))]
    private static partial ScheduledUrl MapToScheduledUrl(DiscoveredUrl discoveredUrl);

    private static ScheduledUrl ToScheduledUrl(DiscoveredUrl discoveredUrl)
    {
        var scheduledUrl = MapToScheduledUrl(discoveredUrl);
        scheduledUrl.Url = discoveredUrl.NormalizedUrl ?? discoveredUrl.Url;
        return scheduledUrl;
    }
    
    public static IEnumerable<ScheduledUrl> ToScheduledUrls(IEnumerable<DiscoveredUrl> discoveredUrls)
    {
        return discoveredUrls.Select(ToScheduledUrl);
    }
}

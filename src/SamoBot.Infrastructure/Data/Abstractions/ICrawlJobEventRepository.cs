namespace SamoBot.Infrastructure.Data.Abstractions;

public interface ICrawlJobEventRepository
{
    Task<long> Append(int crawlJobId, string eventType, string payloadJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlJobEventEntity>> GetAfter(int crawlJobId, long afterId, int limit, CancellationToken cancellationToken = default);
}

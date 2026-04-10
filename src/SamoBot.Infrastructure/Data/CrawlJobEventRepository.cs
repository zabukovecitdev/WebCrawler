using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class CrawlJobEventRepository(QueryFactory queryFactory, TimeProvider timeProvider) : ICrawlJobEventRepository
{
    public async Task<long> Append(int crawlJobId, string eventType, string payloadJson, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.CrawlJobEvents)
            .InsertGetIdAsync<long>(new
            {
                CrawlJobId = crawlJobId,
                EventType = eventType,
                Payload = payloadJson,
                CreatedAt = timeProvider.GetUtcNow()
            }, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CrawlJobEventEntity>> GetAfter(int crawlJobId, long afterId, int limit, CancellationToken cancellationToken = default)
    {
        var rows = await queryFactory.Query(TableNames.Database.CrawlJobEvents)
            .Where(nameof(CrawlJobEventEntity.CrawlJobId), crawlJobId)
            .Where(nameof(CrawlJobEventEntity.Id), ">", afterId)
            .OrderBy(nameof(CrawlJobEventEntity.Id))
            .Limit(limit)
            .GetAsync<CrawlJobEventEntity>(cancellationToken: cancellationToken);
        return rows.ToList();
    }
}

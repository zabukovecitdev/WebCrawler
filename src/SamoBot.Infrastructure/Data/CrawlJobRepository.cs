using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Enums;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class CrawlJobRepository(QueryFactory queryFactory, TimeProvider timeProvider) : ICrawlJobRepository
{
    public async Task<CrawlJobEntity?> GetById(int id, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.CrawlJobs)
            .Where(nameof(CrawlJobEntity.Id), id)
            .FirstOrDefaultAsync<CrawlJobEntity>(cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<CrawlJobEntity>> ListRecent(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await queryFactory.Query(TableNames.Database.CrawlJobs)
            .OrderByDesc(nameof(CrawlJobEntity.CreatedAt))
            .Limit(limit)
            .GetAsync<CrawlJobEntity>(cancellationToken: cancellationToken);
        return rows.ToList();
    }

    public async Task<int> Insert(CrawlJobEntity entity, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        return await queryFactory.Query(TableNames.Database.CrawlJobs)
            .InsertGetIdAsync<int>(new
            {
                entity.OwnerUserId,
                Status = entity.Status,
                entity.SeedUrls,
                entity.MaxDepth,
                entity.MaxUrls,
                entity.UseJsRendering,
                entity.RespectRobots,
                entity.CreatedAt,
                entity.UpdatedAt,
                entity.StartedAt,
                entity.CompletedAt
            }, cancellationToken: cancellationToken);
    }

    public async Task<bool> Update(CrawlJobEntity entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow();
        var affected = await queryFactory.Query(TableNames.Database.CrawlJobs)
            .Where(nameof(CrawlJobEntity.Id), entity.Id)
            .UpdateAsync(new
            {
                entity.OwnerUserId,
                entity.Status,
                entity.SeedUrls,
                entity.MaxDepth,
                entity.MaxUrls,
                entity.UseJsRendering,
                entity.RespectRobots,
                entity.UpdatedAt,
                entity.StartedAt,
                entity.CompletedAt
            }, cancellationToken: cancellationToken);
        return affected > 0;
    }

    public async Task<bool> UpdateStatus(int id, CrawlJobStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.CrawlJobs)
            .Where(nameof(CrawlJobEntity.Id), id)
            .UpdateAsync(new
            {
                Status = status.AsString(),
                UpdatedAt = updatedAt
            }, cancellationToken: cancellationToken);
        return affected > 0;
    }
}

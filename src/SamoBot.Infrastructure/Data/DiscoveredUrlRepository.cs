using System.Data;
using Dapper;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using Samobot.Infrastructure.Enums;
using SamoBot.Infrastructure.Extensions;
using SamoBot.Infrastructure.Models;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class DiscoveredUrlRepository(QueryFactory queryFactory, TimeProvider timeProvider) : IDiscoveredUrlRepository
{
    public async Task<DiscoveredUrl?> GetById(int id, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Id), id)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<IEnumerable<DiscoveredUrl>> GetAll(CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .GetAsync<DiscoveredUrl>(cancellationToken: cancellationToken);
    }

    public async Task<int> Insert(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var id = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .InsertGetIdAsync<int>(new
            {
                entity.Host,
                entity.Url,
                entity.NormalizedUrl,
                DiscoveredAt = entity.DiscoveredAt.ToUniversalTime(),
                entity.Priority
            }, cancellationToken: cancellationToken);

        return id;
    }

    public async Task<bool> Update(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Id), entity.Id)
            .UpdateAsync(new
            {
                entity.Host,
                entity.Url,
                entity.NormalizedUrl,
                Status = entity.Status.AsString(),
                LastCrawlAt = entity.LastCrawlAt?.ToUniversalTime(),
                NextCrawlAt = entity.NextCrawlAt?.ToUniversalTime(),
                entity.FailCount,
                DiscoveredAt = entity.DiscoveredAt.ToUniversalTime(),
                entity.Priority,
                entity.LastFetchId,
                StatusUpdatedAt = entity.StatusUpdatedAt?.ToUniversalTime()
            }, cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Id), id)
            .DeleteAsync(cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<DiscoveredUrl?> GetByUrl(string url, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<bool> Exists(string url, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .ExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<IEnumerable<DiscoveredUrl>> GetReadyForCrawling(int limit, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        // TODO This will fail when there are multiple workers. Needs FOR UPDATE SKIP LOCKED
        return await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Select()
            .Where(nameof(DiscoveredUrl.Status), nameof(UrlStatus.Idle))
            .Where(q => q
                .WhereNull(nameof(DiscoveredUrl.NextCrawlAt))
                .OrWhere(nameof(DiscoveredUrl.NextCrawlAt), "<=", timeProvider.GetUtcNow()))
            .OrderByDesc(nameof(DiscoveredUrl.Priority))
            .OrderBy(nameof(DiscoveredUrl.NextCrawlAt))
            .Limit(limit).GetAsync<DiscoveredUrl>(transaction, cancellationToken: cancellationToken);
    }


    public async Task UpdateStatus(IEnumerable<int> ids, UrlStatus status, IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (transaction?.Connection == null)
        {
            throw new InvalidOperationException("Transaction and connection must be provided");
        }

        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return;
        }

        DateTimeOffset? statusUpdatedAt = status == UrlStatus.InFlight
            ? timeProvider.GetUtcNow().ToUniversalTime()
            : null;

        const string sql = $@"
            UPDATE ""{TableNames.Database.DiscoveredUrls}""
            SET ""Status"" = @Status, ""StatusUpdatedAt"" = @StatusUpdatedAt
            WHERE ""Id"" = ANY(@Ids)";

        var parameters = new
        {
            Status = status.AsString(),
            StatusUpdatedAt = statusUpdatedAt,
            Ids = idList.ToArray()
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);

        await transaction.Connection.ExecuteAsync(command);
    }

    public async Task<IEnumerable<int>> GetStuckInFlightIds(TimeSpan olderThan, int limit, CancellationToken cancellationToken = default)
    {
        var cutoff = timeProvider.GetUtcNow().Subtract(olderThan);
        var rows = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Select(nameof(DiscoveredUrl.Id))
            .Where(nameof(DiscoveredUrl.Status), nameof(UrlStatus.InFlight))
            .Where(q => q
                .WhereNull(nameof(DiscoveredUrl.StatusUpdatedAt))
                .OrWhere(nameof(DiscoveredUrl.StatusUpdatedAt), "<", cutoff.ToUniversalTime()))
            .Limit(limit)
            .GetAsync<DiscoveredUrl>(cancellationToken: cancellationToken);
        return rows.Select(r => r.Id);
    }

    public async Task<int> ResetOrphanedInFlightToIdle(IEnumerable<int> ids, DateTimeOffset nextCrawlAt, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
        {
            return 0;
        }

        var updateFields = new Dictionary<string, object?>
        {
            { nameof(DiscoveredUrl.Status), nameof(UrlStatus.Idle) },
            { nameof(DiscoveredUrl.NextCrawlAt), nextCrawlAt.ToUniversalTime() },
            { nameof(DiscoveredUrl.StatusUpdatedAt), null }
        };

        var query = queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .WhereIn(nameof(DiscoveredUrl.Id), idList)
            .Where(nameof(DiscoveredUrl.Status), nameof(UrlStatus.InFlight));

        var affected = await query.UpdateAsync(updateFields, cancellationToken: cancellationToken);
        return affected;
    }

    public async Task<bool> UpdateAfterFetch(int discoveredUrlId, int? fetchId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var updateFields = new Dictionary<string, object?>
        {
            { nameof(DiscoveredUrl.LastCrawlAt), now.ToUniversalTime() },
            { nameof(DiscoveredUrl.NextCrawlAt), now.AddDays(1).ToUniversalTime() },
            { nameof(DiscoveredUrl.Status), nameof(UrlStatus.Idle) }
        };

        if (fetchId.HasValue)
        {
            updateFields[nameof(DiscoveredUrl.LastFetchId)] = fetchId.Value;
        }

        var affected = await queryFactory.Query(TableNames.Database.DiscoveredUrls)
            .Where(nameof(DiscoveredUrl.Id), discoveredUrlId)
            .UpdateAsync(updateFields, cancellationToken: cancellationToken);

        return affected > 0;
    }
}

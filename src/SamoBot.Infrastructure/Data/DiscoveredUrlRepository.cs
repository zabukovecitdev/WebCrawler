using System.Data;
using Dapper;
using Samobot.Domain.Enums;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Extensions;
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
                entity.LastStatusCode,
                DiscoveredAt = entity.DiscoveredAt.ToUniversalTime(),
                entity.Priority,
                entity.ContentType,
                entity.ContentLength,
                entity.ObjectName
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
        // TODO This will fail ehen there are multiple workers. Needs FOR UPDATE SKIP LOCKED
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

    public async Task UpdateStatusToInFlight(IEnumerable<int> ids, IDbTransaction transaction, CancellationToken cancellationToken = default)
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

        var sql = $@"
            UPDATE ""{TableNames.Database.DiscoveredUrls}""
            SET ""Status"" = @Status
            WHERE ""Id"" = ANY(@Ids)";

        var parameters = new
        {
            Status = nameof(UrlStatus.InFlight),
            Ids = idList.ToArray()
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        
        await transaction.Connection.ExecuteAsync(command);
    }
}

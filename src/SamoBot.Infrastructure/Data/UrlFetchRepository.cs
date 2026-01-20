using Samobot.Domain.Models;
using SamoBot.Infrastructure.Constants;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class UrlFetchRepository(QueryFactory queryFactory) : IUrlFetchRepository
{
    public async Task<UrlFetch?> GetById(int id, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), id)
            .FirstOrDefaultAsync<UrlFetch>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<IEnumerable<UrlFetch>> GetAll(CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableNames.Database.UrlFetches)
            .GetAsync<UrlFetch>(cancellationToken: cancellationToken);
    }

    public async Task<int> Insert(UrlFetch entity, CancellationToken cancellationToken = default)
    {
        var id = await queryFactory.Query(TableNames.Database.UrlFetches)
            .InsertGetIdAsync<int>(new
            {
                entity.DiscoveredUrlId,
                FetchedAt = entity.FetchedAt.ToUniversalTime(),
                entity.StatusCode,
                entity.ContentType,
                entity.ContentLength,
                entity.ObjectName
            }, cancellationToken: cancellationToken);

        return id;
    }

    public async Task<bool> Update(UrlFetch entity, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), entity.Id)
            .UpdateAsync(new
            {
                entity.DiscoveredUrlId,
                FetchedAt = entity.FetchedAt.ToUniversalTime(),
                entity.StatusCode,
                entity.ContentType,
                entity.ContentLength,
                entity.ObjectName
            }, cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableNames.Database.UrlFetches)
            .Where(nameof(UrlFetch.Id), id)
            .DeleteAsync(cancellationToken: cancellationToken);

        return affected > 0;
    }
}

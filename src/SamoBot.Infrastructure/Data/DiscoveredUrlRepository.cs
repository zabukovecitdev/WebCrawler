using Samobot.Domain.Models;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class DiscoveredUrlRepository(QueryFactory queryFactory) : IDiscoveredUrlRepository
{
    private const string TableName = "DiscoveredUrls";

    public async Task<DiscoveredUrl?> GetById(int id, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), id)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<IEnumerable<DiscoveredUrl>> GetAll(CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableName)
            .GetAsync<DiscoveredUrl>(cancellationToken: cancellationToken);
    }

    public async Task<int> Insert(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var id = await queryFactory.Query(TableName)
            .InsertGetIdAsync<int>(new
            {
                entity.Host,
                entity.Url,
                entity.NormalizedUrl,
                entity.DiscoveredAt,
                entity.Priority
            }, cancellationToken: cancellationToken);

        return id;
    }

    public async Task<bool> Update(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), entity.Id)
            .UpdateAsync(entity, cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        var affected = await queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), id)
            .DeleteAsync(cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<DiscoveredUrl?> GetByUrl(string url, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<bool> Exists(string url, CancellationToken cancellationToken = default)
    {
        return await queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .ExistsAsync(cancellationToken: cancellationToken);
    }
}

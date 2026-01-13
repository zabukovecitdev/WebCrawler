using SamoBot.Infrastructure.Models;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class DiscoveredUrlRepository(QueryFactory queryFactory) : IDiscoveredUrlRepository
{
    private const string TableName = "DiscoveredUrls";
    private readonly QueryFactory _queryFactory = queryFactory;

    public async Task<DiscoveredUrl?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), id)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<IEnumerable<DiscoveredUrl>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _queryFactory.Query(TableName)
            .GetAsync<DiscoveredUrl>(cancellationToken: cancellationToken);
    }

    public async Task<int> InsertAsync(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var id = await _queryFactory.Query(TableName)
            .InsertGetIdAsync<int>(new
            {
                entity.Url,
                entity.NormalizedUrl,
                entity.DiscoveredAt
            }, cancellationToken: cancellationToken);

        return id;
    }

    public async Task<bool> UpdateAsync(DiscoveredUrl entity, CancellationToken cancellationToken = default)
    {
        var affected = await _queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), entity.Id)
            .UpdateAsync(entity, cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var affected = await _queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Id), id)
            .DeleteAsync(cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<DiscoveredUrl?> GetByUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var result = await _queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .FirstOrDefaultAsync<DiscoveredUrl>(cancellationToken: cancellationToken);

        return result;
    }

    public async Task<bool> ExistsAsync(string url, CancellationToken cancellationToken = default)
    {
        return await _queryFactory.Query(TableName)
            .Where(nameof(DiscoveredUrl.Url), url)
            .OrWhere(nameof(DiscoveredUrl.NormalizedUrl), url)
            .ExistsAsync(cancellationToken: cancellationToken);
    }
}

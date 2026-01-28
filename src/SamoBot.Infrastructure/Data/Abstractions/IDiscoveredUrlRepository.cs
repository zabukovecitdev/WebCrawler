using System.Data;
using Samobot.Domain.Enums;
using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface IDiscoveredUrlRepository : IRepository<DiscoveredUrl>
{
    Task<DiscoveredUrl?> GetByUrl(string url, CancellationToken cancellationToken = default);
    Task<bool> Exists(string url, CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscoveredUrl>> GetReadyForCrawling(int limit, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task UpdateStatus(IEnumerable<int> ids, UrlStatus status, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> UpdateAfterFetch(int discoveredUrlId, int? fetchId, CancellationToken cancellationToken = default);
}

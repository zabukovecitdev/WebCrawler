using System.Data;
using Samobot.Domain.Enums;
using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Data;

public interface IDiscoveredUrlRepository : IRepository<DiscoveredUrl>
{
    Task<DiscoveredUrl?> GetByUrl(string url, CancellationToken cancellationToken = default);
    Task<bool> Exists(string url, CancellationToken cancellationToken = default);
    Task<IEnumerable<DiscoveredUrl>> GetReadyForCrawling(int limit, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task UpdateStatusToInFlight(IEnumerable<int> ids, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task<bool> UpdateAfterFetch(int discoveredUrlId, int fetchId, CancellationToken cancellationToken = default);
}

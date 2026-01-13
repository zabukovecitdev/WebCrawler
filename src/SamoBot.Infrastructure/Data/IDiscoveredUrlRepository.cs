using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Data;

public interface IDiscoveredUrlRepository : IRepository<DiscoveredUrl>
{
    Task<DiscoveredUrl?> GetByUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string url, CancellationToken cancellationToken = default);
}

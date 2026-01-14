using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Data;

public interface IDiscoveredUrlRepository : IRepository<DiscoveredUrl>
{
    Task<DiscoveredUrl?> GetByUrl(string url, CancellationToken cancellationToken = default);
    Task<bool> Exists(string url, CancellationToken cancellationToken = default);
}

using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Data;

public interface IUrlFetchRepository : IRepository<UrlFetch>
{
    // Additional query methods can be added here when needed
    // For example: GetLatestByDiscoveredUrlId, GetByDiscoveredUrlId, etc.
}

using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Abstractions;

public interface ISchedulerService
{
    Task<IEnumerable<DiscoveredUrl>> GetScheduledEntities(int limit, CancellationToken cancellationToken = default);
}

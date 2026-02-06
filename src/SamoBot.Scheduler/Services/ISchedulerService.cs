using SamoBot.Infrastructure.Models;

namespace SamoBot.Scheduler.Services;

public interface ISchedulerService
{
    Task<IEnumerable<DiscoveredUrl>> GetScheduledEntities(int limit, CancellationToken cancellationToken = default);
}

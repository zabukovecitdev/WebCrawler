using Samobot.Domain.Models;

namespace SamoBot.Scheduler.Services;

public interface ISchedulerService
{
    Task<IEnumerable<DiscoveredUrl>> GetScheduledEntities(uint limit, CancellationToken cancellationToken = default);
}

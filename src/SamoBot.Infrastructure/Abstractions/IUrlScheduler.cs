using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Abstractions;

public interface IUrlScheduler
{
    Task Publish(IEnumerable<DiscoveredUrl> urls, CancellationToken cancellationToken = default);
    Task PublishBatch(IEnumerable<DiscoveredUrl> urls, CancellationToken cancellationToken = default);
}

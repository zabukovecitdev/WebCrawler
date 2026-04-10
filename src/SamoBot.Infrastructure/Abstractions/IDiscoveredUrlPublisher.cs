using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Abstractions;

public interface IDiscoveredUrlPublisher
{
    Task PublishUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);

    Task PublishDiscoveriesAsync(IEnumerable<UrlDiscoveryMessage> discoveries, CancellationToken cancellationToken = default);
}

namespace SamoBot.Infrastructure.Abstractions;

public interface IDiscoveredUrlPublisher
{
    Task PublishUrlsAsync(IEnumerable<string> urls, CancellationToken cancellationToken = default);
}

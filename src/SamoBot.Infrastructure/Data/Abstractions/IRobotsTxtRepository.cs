using RobotsTxtModel = Samobot.Domain.Models.RobotsTxt;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface IRobotsTxtRepository
{
    /// <summary>
    /// Gets robots.txt for a host if it exists and hasn't expired.
    /// </summary>
    Task<RobotsTxtModel?> GetByHostAsync(string host, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves or updates robots.txt for a host.
    /// </summary>
    Task SaveAsync(RobotsTxtModel robotsTxt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets robots.txt entries that are expiring within the specified timespan.
    /// </summary>
    Task<List<RobotsTxtModel>> GetExpiringAsync(TimeSpan expiresWithin, int limit, CancellationToken cancellationToken = default);
}

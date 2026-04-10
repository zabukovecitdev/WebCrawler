using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Enums;

namespace SamoBot.Infrastructure.Services.Abstractions;

public interface ICrawlJobService
{
    Task<CrawlJobEntity> CreateAsync(
        string? ownerUserId,
        IReadOnlyList<string> seedUrls,
        int? maxDepth,
        int? maxUrls,
        bool useJsRendering,
        bool respectRobots,
        CancellationToken cancellationToken = default);

    Task<bool> StartAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> PauseAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(int id, CancellationToken cancellationToken = default);
    Task<CrawlJobEntity?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<CrawlJobStatus?> GetStatusAsync(int id, CancellationToken cancellationToken = default);
}

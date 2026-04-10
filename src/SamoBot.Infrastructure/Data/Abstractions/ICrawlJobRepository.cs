using SamoBot.Infrastructure.Enums;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface ICrawlJobRepository
{
    Task<CrawlJobEntity?> GetById(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrawlJobEntity>> ListRecent(int limit, CancellationToken cancellationToken = default);
    Task<int> Insert(CrawlJobEntity entity, CancellationToken cancellationToken = default);
    Task<bool> Update(CrawlJobEntity entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatus(int id, CrawlJobStatus status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default);
}

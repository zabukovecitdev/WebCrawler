using FluentResults;

namespace SamoBot.Infrastructure.Abstractions;

public interface ICache
{
    Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken = default);

    Task<Result> SetAsync(string key, string value, TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    Task<Result<(bool Allowed, long NextAllowedTimestamp)>> TryClaimNextCrawl(string key, long currentTimestamp,
        long delayMs, CancellationToken cancellationToken = default);

    Task<Result<bool>> Remove(string key, CancellationToken cancellationToken = default);
    Task<Result<string?>> ZPopMinAsync(string key, CancellationToken cancellationToken = default);
    Task<Result> EnqueueUrlForCrawl(string url, long dueTimestamp, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> Dequeue(long currentTimestamp, int maxCount = 100,
        CancellationToken cancellationToken = default);
}
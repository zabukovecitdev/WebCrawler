using FluentResults;

namespace SamoBot.Infrastructure.Abstractions;

/// <summary>
/// Interface for cache operations using Redis
/// </summary>
public interface ICache
{
    /// <summary>
    /// Gets a value from the cache by key
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the cached value, or null if the key doesn't exist</returns>
    Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a key-value pair in the cache with an optional time-to-live (TTL)
    /// </summary>
    /// <param name="key">The cache key</param>
    /// <param name="value">The value to store</param>
    /// <param name="ttl">Optional time-to-live. If null, the key will not expire</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key from the cache
    /// </summary>
    /// <param name="key">The cache key to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing true if the key was removed, false if it didn't exist</returns>
    Task<Result<bool>> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes and returns the member with the lowest score from a sorted set
    /// </summary>
    /// <param name="key">The sorted set key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the member with the lowest score, or null if the sorted set is empty</returns>
    Task<Result<string?>> ZPopMinAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a URL to the due queue with a timestamp score indicating when it becomes due
    /// </summary>
    /// <param name="url">The URL to enqueue</param>
    /// <param name="dueTimestamp">Unix timestamp in milliseconds when the URL becomes due</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> EnqueueDueAsync(string url, long dueTimestamp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically retrieves and removes all URLs from the due queue that are due (score <= current timestamp)
    /// </summary>
    /// <param name="currentTimestamp">Current Unix timestamp in milliseconds</param>
    /// <param name="maxCount">Maximum number of items to dequeue (default: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing list of URLs that were dequeued</returns>
    Task<Result<IReadOnlyList<string>>> DequeueDueAsync(long currentTimestamp, int maxCount = 100, CancellationToken cancellationToken = default);
}

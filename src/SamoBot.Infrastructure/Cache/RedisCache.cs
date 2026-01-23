using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Utilities;

namespace SamoBot.Infrastructure.Cache;

public class RedisCache : ICache
{
    private readonly IConnectionMultiplexer? _connectionMultiplexer;
    private readonly ILogger<RedisCache> _logger;
    private readonly RedisOptions _options;

    public RedisCache(
        IConnectionMultiplexer? connectionMultiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisCache> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _options = options.Value;
        _logger = logger;
    }

    private IDatabase? GetDatabase()
    {
        if (_connectionMultiplexer == null || !_connectionMultiplexer.IsConnected)
        {
            _logger.LogWarning("Redis connection is not available");
            
            return null;
        }

        return _connectionMultiplexer.GetDatabase(_options.Database);
    }

    public async Task<Result<string?>> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot get cache key {Key}: {Error}", key, error);
            
            return Result.Fail(error);
        }

        try
        {
            var value = await database.StringGetAsync(key);
            
            if (!value.HasValue)
            {
                _logger.LogDebug("Cache key {Key} not found", key);
                return Result.Ok<string?>(null);
            }

            var result = value.ToString();
            _logger.LogDebug("Retrieved cache key {Key}", key);
            
            return Result.Ok<string?>(result);
        }
        catch (Exception ex)
        {
            var error = $"Error getting cache key {key}: {ex.Message}";
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            
            return Result.Fail(error);
        }
    }

    public async Task<Result> SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot set cache key {Key}: {Error}", key, error);
            return Result.Fail(error);
        }

        try
        {
            if (ttl.HasValue)
            {
                await database.StringSetAsync(key, value, ttl.Value);
            }
            else
            {
                await database.StringSetAsync(key, value);
            }

            _logger.LogDebug("Set cache key {Key} with TTL {Ttl}", key, ttl);
            
            return Result.Ok();
        }
        catch (Exception ex)
        {
            var error = $"Error setting cache key {key}: {ex.Message}";
            _logger.LogError(ex, "Error setting cache key {Key}", key);
            return Result.Fail(error);
        }
    }

    public async Task<Result<(bool Allowed, long NextAllowedTimestamp)>> TryClaimNextCrawl(
        string key,
        long currentTimestamp,
        long delayMs,
        CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot claim next crawl for key {Key}: {Error}", key, error);
            
            return Result.Fail(error);
        }

        try
        {
            const string luaScript = @"
                local key = KEYS[1]
                local now = tonumber(ARGV[1])
                local delay = tonumber(ARGV[2])
                local next = redis.call('GET', key)

                if not next then
                    local new_next = now + delay
                    redis.call('SET', key, new_next)
                    return {1, new_next}
                end

                local next_num = tonumber(next)
                if (not next_num) or (next_num <= now) then
                    local new_next = now + delay
                    redis.call('SET', key, new_next)
                    return {1, new_next}
                end

                return {0, next_num}
            ";

            var result = await database.ScriptEvaluateAsync(
                luaScript,
                [key],
                [currentTimestamp, delayMs]);

            if (result.IsNull)
            {
                var error = "Redis script returned null for next crawl claim";
                _logger.LogWarning("Cannot claim next crawl for key {Key}: {Error}", key, error);
                return Result.Fail(error);
            }

            var values = (RedisValue[])result!;
            if (values.Length < 2)
            {
                var error = "Redis script returned unexpected result for next crawl claim";
                _logger.LogWarning("Cannot claim next crawl for key {Key}: {Error}", key, error);
                return Result.Fail(error);
            }

            if (!long.TryParse(values[0].ToString(), out var allowedFlag))
            {
                var error = "Redis script returned invalid allowed flag for next crawl claim";
                _logger.LogWarning("Cannot claim next crawl for key {Key}: {Error}", key, error);
                
                return Result.Fail(error);
            }

            if (!long.TryParse(values[1].ToString(), out var nextAllowedTimestamp))
            {
                var error = "Redis script returned invalid timestamp for next crawl claim";
                _logger.LogWarning("Cannot claim next crawl for key {Key}: {Error}", key, error);
                
                return Result.Fail(error);
            }

            var allowed = allowedFlag == 1;
            _logger.LogDebug(
                "Next crawl claim for key {Key}: allowed={Allowed}, next={NextAllowedTimestamp}",
                key,
                allowed,
                nextAllowedTimestamp);

            return Result.Ok((allowed, nextAllowedTimestamp));
        }
        catch (Exception ex)
        {
            var error = $"Error claiming next crawl for key {key}: {ex.Message}";
            _logger.LogError(ex, "Error claiming next crawl for key {Key}", key);
            
            return Result.Fail(error);
        }
    }

    public async Task<Result<bool>> Remove(string key, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot remove cache key {Key}: {Error}", key, error);
            return Result.Fail(error);
        }

        try
        {
            var result = await database.KeyDeleteAsync(key);
            _logger.LogDebug("Removed cache key {Key}: {Result}", key, result);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            var error = $"Error removing cache key {key}: {ex.Message}";
            _logger.LogError(ex, "Error removing cache key {Key}", key);
            return Result.Fail(error);
        }
    }

    public async Task<Result<string?>> ZPopMinAsync(string key, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot ZPOPMIN from key {Key}: {Error}", key, error);
            
            return Result.Fail(error);
        }

        try
        {
            var result = await database.SortedSetPopAsync(key, order: Order.Ascending, count: 1);
            
            if (result.Length == 0)
            {
                _logger.LogDebug("ZPOPMIN from key {Key}: sorted set is empty", key);
                
                return Result.Ok((string?)null);
            }

            var member = result[0].Element.ToString();
            _logger.LogDebug("ZPOPMIN from key {Key}: returned {Member}", key, member);
            
            return Result.Ok((string?)member);
        }
        catch (Exception ex)
        {
            var error = $"Error performing ZPOPMIN on key {key}: {ex.Message}";
            _logger.LogError(ex, "Error performing ZPOPMIN on key {Key}", key);
            
            return Result.Fail(error);
        }
    }

    public async Task<Result> EnqueueUrlForCrawl(string url, long dueTimestamp, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot enqueue URL to due queue: {Error}", error);
            
            return Result.Fail(error);
        }

        try
        {
            var queueKey = CacheKey.DueQueue();
            await database.SortedSetAddAsync(queueKey, url, dueTimestamp);
            _logger.LogDebug("Enqueued URL {Url} to due queue with timestamp {Timestamp}", url, dueTimestamp);
            
            return Result.Ok();
        }
        catch (Exception ex)
        {
            var error = $"Error enqueueing URL {url} to due queue: {ex.Message}";
            _logger.LogError(ex, "Error enqueueing URL to due queue");
            
            return Result.Fail(error);
        }
    }

    public async Task<Result<IReadOnlyList<string>>> Dequeue(long currentTimestamp, int maxCount = 100, CancellationToken cancellationToken = default)
    {
        var database = GetDatabase();
        if (database == null)
        {
            var error = "Redis connection is not available";
            _logger.LogWarning("Cannot dequeue from due queue: {Error}", error);
            
            return Result.Fail(error);
        }

        try
        {
            var queueKey = CacheKey.DueQueue();
            
            const string luaScript = @"
                local key = KEYS[1]
                local max_score = tonumber(ARGV[1])
                local limit = tonumber(ARGV[2])
                
                -- Get items with score <= max_score, ordered by score ascending
                local items = redis.call('ZRANGEBYSCORE', key, '-inf', max_score, 'LIMIT', 0, limit)
                
                if #items > 0 then
                    -- Remove the items we're returning
                    redis.call('ZREM', key, unpack(items))
                end
                
                return items
            ";

            var result = await database.ScriptEvaluateAsync(
                luaScript,
                [queueKey],
                [currentTimestamp, maxCount]);

            if (result.IsNull)
            {
                _logger.LogDebug("No due items found in queue");
                return Result.Ok<IReadOnlyList<string>>(Array.Empty<string>());
            }

            var items = (RedisValue[])result!;
            var urls = items.Select(item => item.ToString()).ToList();
            
            _logger.LogDebug("Dequeued {Count} URLs from due queue", urls.Count);
            return Result.Ok<IReadOnlyList<string>>(urls);
        }
        catch (Exception ex)
        {
            var error = $"Error dequeuing from due queue: {ex.Message}";
            _logger.LogError(ex, "Error dequeuing from due queue");
            return Result.Fail(error);
        }
    }
}

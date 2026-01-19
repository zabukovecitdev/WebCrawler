using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Services;

public interface IDomainRateLimiter
{
    Task WaitForDomainDelayAsync(string url, CancellationToken cancellationToken = default);
    Task RecordRequestAsync(string url);
    Task RecordRetryAfterAsync(string url, TimeSpan retryAfter);
}

internal class InMemoryRateLimitEntry
{
    public DateTimeOffset NextAllowedRequest { get; set; }
    public DateTimeOffset? RetryAfterUntil { get; set; }
}

public class DomainRateLimiter : IDomainRateLimiter
{
    private const string DelayKeyPrefix = "ratelimit:delay:";
    private const string RetryAfterKeyPrefix = "ratelimit:retryafter:";
    
    private readonly IConnectionMultiplexer? _redis;
    private readonly CrawlerOptions _options;
    private readonly ILogger<DomainRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;
    
    // In-memory fallback when Redis is not available
    private readonly ConcurrentDictionary<string, InMemoryRateLimitEntry> _inMemoryRateLimits = new();

    public DomainRateLimiter(
        IConnectionMultiplexer? redis,
        IOptions<CrawlerOptions> options,
        ILogger<DomainRateLimiter> logger,
        TimeProvider timeProvider)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
        
        if (redis == null || !redis.IsConnected)
        {
            _logger.LogWarning("Redis is not available. Using in-memory rate limiting. Rate limits will not be shared across instances.");
        }
    }

    private bool IsRedisAvailable()
    {
        return _redis != null && _redis.IsConnected;
    }

    private IDatabase? GetDatabase()
    {
        return IsRedisAvailable() ? _redis!.GetDatabase() : null;
    }
    
    public async Task WaitForDomainDelayAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.UsePerDomainRateLimiting)
        {
            await Task.Delay(_options.DefaultDelayMs, cancellationToken);
            return;
        }

        var domain = ExtractDomain(url);
        var now = _timeProvider.GetUtcNow();

        var database = GetDatabase();
        if (database != null)
        {
            await WaitForDomainDelayRedisAsync(domain, now, database, cancellationToken);
        }
        else
        {
            await WaitForDomainDelayInMemoryAsync(domain, now, cancellationToken);
        }
    }

    private async Task WaitForDomainDelayRedisAsync(string domain, DateTimeOffset now, IDatabase database, CancellationToken cancellationToken)
    {

        // Check Retry-After header first (higher priority)
        var retryAfterKey = RetryAfterKeyPrefix + domain;
        var retryAfterUntilValue = await database.StringGetAsync(retryAfterKey);
        
        if (retryAfterUntilValue.HasValue && long.TryParse(retryAfterUntilValue.ToString(), out var retryAfterUntilTicks))
        {
            var retryAfterUntil = new DateTimeOffset(retryAfterUntilTicks, TimeSpan.Zero);
            if (now < retryAfterUntil)
            {
                var retryAfterDelay = retryAfterUntil - now;
                var delayMs = (int)Math.Min(retryAfterDelay.TotalMilliseconds, _options.MaxRetryAfterSeconds * 1000);
                
                _logger.LogInformation(
                    "Respecting Retry-After header for domain {Domain}, waiting {DelayMs}ms",
                    domain, delayMs);
                
                await Task.Delay(delayMs, cancellationToken);
                now = _timeProvider.GetUtcNow();
            }
            else
            {
                // Retry-After period has expired, remove it
                await database.KeyDeleteAsync(retryAfterKey);
            }
        }

        // TTL-based delay mechanism: try to set key with TTL (only if it doesn't exist)
        var delayKey = DelayKeyPrefix + domain;
        var delayTtl = TimeSpan.FromMilliseconds(_options.DefaultDelayMs);
        
        // Try to atomically set the key with TTL only if it doesn't exist
        var wasSet = await database.StringSetAsync(
            delayKey,
            "1",
            delayTtl,
            When.NotExists);
        
        if (!wasSet)
        {
            var remainingTtl = await database.KeyTimeToLiveAsync(delayKey);
            
            if (remainingTtl.HasValue && remainingTtl.Value.TotalMilliseconds > 0)
            {
                var delayMs = (int)Math.Clamp(
                    remainingTtl.Value.TotalMilliseconds,
                    _options.MinDelayMs,
                    _options.MaxDelayMs);
                
                _logger.LogDebug(
                    "Rate limiting domain {Domain}, waiting {DelayMs}ms (TTL remaining: {RemainingMs}ms)",
                    domain, delayMs, remainingTtl.Value.TotalMilliseconds);
                
                await Task.Delay(delayMs, cancellationToken);
                
                // After delay, set the key again with TTL for the next request
                await database.StringSetAsync(delayKey, "1", delayTtl);
            }
            else
            {
                // TTL expired between check and now, set it for next request
                await database.StringSetAsync(delayKey, "1", delayTtl);
            }
        }
        else
        {
            // Key was set (first request or TTL expired) - proceed immediately
            // Key is already set with TTL, so next request will be delayed
            _logger.LogDebug(
                "No delay needed for domain {Domain} (first request or TTL expired)",
                domain);
        }
    }

    private async Task WaitForDomainDelayInMemoryAsync(string domain, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var entry = _inMemoryRateLimits.GetOrAdd(domain, _ => new InMemoryRateLimitEntry
        {
            NextAllowedRequest = DateTimeOffset.MinValue
        });

        // Check Retry-After header first (higher priority)
        if (entry.RetryAfterUntil.HasValue && now < entry.RetryAfterUntil.Value)
        {
            var retryAfterDelay = entry.RetryAfterUntil.Value - now;
            var delayMs = (int)Math.Min(retryAfterDelay.TotalMilliseconds, _options.MaxRetryAfterSeconds * 1000);
            
            _logger.LogInformation(
                "Respecting Retry-After header for domain {Domain}, waiting {DelayMs}ms (in-memory)",
                domain, delayMs);
            
            await Task.Delay(delayMs, cancellationToken);
            now = _timeProvider.GetUtcNow();
            entry.RetryAfterUntil = null; // Clear after delay
        }

        // Check if we need to delay based on rate limit
        if (now < entry.NextAllowedRequest)
        {
            var delay = entry.NextAllowedRequest - now;
            var delayMs = (int)Math.Clamp(
                delay.TotalMilliseconds,
                _options.MinDelayMs,
                _options.MaxDelayMs);
            
            _logger.LogDebug(
                "Rate limiting domain {Domain}, waiting {DelayMs}ms (in-memory)",
                domain, delayMs);
            
            await Task.Delay(delayMs, cancellationToken);
            now = _timeProvider.GetUtcNow();
        }

        // Set next allowed request time
        entry.NextAllowedRequest = now.AddMilliseconds(_options.DefaultDelayMs);
        
        _logger.LogDebug(
            "No delay needed for domain {Domain} (in-memory)",
            domain);
    }

    public async Task RecordRequestAsync(string url)
    {
        // With TTL-based approach, the delay key is already set in WaitForDomainDelayAsync
        // This method is kept for backward compatibility but is essentially a no-op
        // when using TTL-based rate limiting
        if (!_options.UsePerDomainRateLimiting)
        {
            return;
        }

        // The TTL key is already set in WaitForDomainDelayAsync, so nothing to do here
        // This ensures the delay is enforced before the request, not after
    }

    public async Task RecordRetryAfterAsync(string url, TimeSpan retryAfter)
    {
        if (!_options.RespectRetryAfter)
        {
            return;
        }

        var domain = ExtractDomain(url);
        var maxRetryAfter = TimeSpan.FromSeconds(_options.MaxRetryAfterSeconds);
        var actualRetryAfter = retryAfter > maxRetryAfter ? maxRetryAfter : retryAfter;
        var retryAfterUntil = _timeProvider.GetUtcNow().Add(actualRetryAfter);

        var database = GetDatabase();
        if (database != null)
        {
            var retryAfterKey = RetryAfterKeyPrefix + domain;
            
            // Store with expiration matching the retry-after duration
            await database.StringSetAsync(
                retryAfterKey,
                retryAfterUntil.UtcTicks.ToString(),
                actualRetryAfter);
        }
        else
        {
            // In-memory fallback
            var entry = _inMemoryRateLimits.GetOrAdd(domain, _ => new InMemoryRateLimitEntry
            {
                NextAllowedRequest = DateTimeOffset.MinValue
            });
            entry.RetryAfterUntil = retryAfterUntil;
        }
        
        _logger.LogWarning(
            "Recorded Retry-After for domain {Domain}: {RetryAfterSeconds}s (until {Until})",
            domain, actualRetryAfter.TotalSeconds, retryAfterUntil);
    }

    private static string ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host.ToLowerInvariant();
        }
        catch
        {
            // If URL parsing fails, return a default key
            return "unknown";
        }
    }
}

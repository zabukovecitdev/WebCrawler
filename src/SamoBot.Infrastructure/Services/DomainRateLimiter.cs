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

public class DomainRateLimiter : IDomainRateLimiter
{
    private const string LastRequestKeyPrefix = "ratelimit:lastrequest:";
    private const string RetryAfterKeyPrefix = "ratelimit:retryafter:";
    
    private readonly IDatabase _database;
    private readonly CrawlerOptions _options;
    private readonly ILogger<DomainRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;

    public DomainRateLimiter(
        IConnectionMultiplexer redis,
        IOptions<CrawlerOptions> options,
        ILogger<DomainRateLimiter> logger,
        TimeProvider timeProvider)
    {
        _database = redis.GetDatabase();
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
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

        var retryAfterKey = RetryAfterKeyPrefix + domain;
        var retryAfterUntilValue = await _database.StringGetAsync(retryAfterKey);
        
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
                await _database.KeyDeleteAsync(retryAfterKey);
            }
        }

        // Check last request time for this domain
        var lastRequestKey = LastRequestKeyPrefix + domain;
        var lastRequestTimeValue = await _database.StringGetAsync(lastRequestKey);
        
        if (lastRequestTimeValue.HasValue && long.TryParse(lastRequestTimeValue.ToString(), out var lastRequestTimeTicks))
        {
            var lastRequestTime = new DateTimeOffset(lastRequestTimeTicks, TimeSpan.Zero);
            var timeSinceLastRequest = now - lastRequestTime;
            var requiredDelay = TimeSpan.FromMilliseconds(_options.DefaultDelayMs);
            
            if (timeSinceLastRequest < requiredDelay)
            {
                var delayNeeded = requiredDelay - timeSinceLastRequest;
                var delayMs = (int)Math.Clamp(
                    delayNeeded.TotalMilliseconds,
                    _options.MinDelayMs,
                    _options.MaxDelayMs);
                
                _logger.LogDebug(
                    "Rate limiting domain {Domain}, waiting {DelayMs}ms since last request",
                    domain, delayMs);
                
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    public async Task RecordRequestAsync(string url)
    {
        if (!_options.UsePerDomainRateLimiting)
        {
            return;
        }

        var domain = ExtractDomain(url);
        var lastRequestKey = LastRequestKeyPrefix + domain;
        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        
        // Store with expiration (clean up after 24 hours of inactivity)
        await _database.StringSetAsync(
            lastRequestKey, 
            nowTicks.ToString(), 
            TimeSpan.FromHours(24));
    }

    public async Task RecordRetryAfterAsync(string url, TimeSpan retryAfter)
    {
        if (!_options.RespectRetryAfter)
        {
            return;
        }

        var domain = ExtractDomain(url);
        var retryAfterKey = RetryAfterKeyPrefix + domain;
        var maxRetryAfter = TimeSpan.FromSeconds(_options.MaxRetryAfterSeconds);
        var actualRetryAfter = retryAfter > maxRetryAfter ? maxRetryAfter : retryAfter;
        var retryAfterUntil = _timeProvider.GetUtcNow().Add(actualRetryAfter);
        
        // Store with expiration matching the retry-after duration
        await _database.StringSetAsync(
            retryAfterKey,
            retryAfterUntil.UtcTicks.ToString(),
            actualRetryAfter);
        
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

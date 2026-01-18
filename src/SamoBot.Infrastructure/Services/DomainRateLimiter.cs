using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Services;

public interface IDomainRateLimiter
{
    /// <summary>
    /// Waits for the appropriate delay before making a request to the given URL's domain
    /// </summary>
    Task WaitForDomainDelayAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a request was made to the given URL's domain
    /// </summary>
    void RecordRequest(string url);

    /// <summary>
    /// Records a Retry-After delay for a domain
    /// </summary>
    void RecordRetryAfter(string url, TimeSpan retryAfter);
}

public class DomainRateLimiter : IDomainRateLimiter
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequestTime = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _retryAfterUntil = new();
    private readonly CrawlerOptions _options;
    private readonly ILogger<DomainRateLimiter> _logger;
    private readonly TimeProvider _timeProvider;

    public DomainRateLimiter(
        IOptions<CrawlerOptions> options,
        ILogger<DomainRateLimiter> logger,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }
    
    public async Task WaitForDomainDelayAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!_options.UsePerDomainRateLimiting)
        {
            // If per-domain rate limiting is disabled, use default delay
            await Task.Delay(_options.DefaultDelayMs, cancellationToken);
            return;
        }

        var domain = ExtractDomain(url);
        var now = _timeProvider.GetUtcNow();

        // Check if there's a Retry-After delay in effect
        if (_retryAfterUntil.TryGetValue(domain, out var retryAfterUntil))
        {
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
                _retryAfterUntil.TryRemove(domain, out _);
            }
        }

        // Check last request time for this domain
        if (_lastRequestTime.TryGetValue(domain, out var lastRequestTime))
        {
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

    public void RecordRequest(string url)
    {
        if (!_options.UsePerDomainRateLimiting)
        {
            return;
        }

        var domain = ExtractDomain(url);
        _lastRequestTime.AddOrUpdate(domain, _timeProvider.GetUtcNow(), (_, _) => _timeProvider.GetUtcNow());
    }

    public void RecordRetryAfter(string url, TimeSpan retryAfter)
    {
        if (!_options.RespectRetryAfter)
        {
            return;
        }

        var domain = ExtractDomain(url);
        var maxRetryAfter = TimeSpan.FromSeconds(_options.MaxRetryAfterSeconds);
        var actualRetryAfter = retryAfter > maxRetryAfter ? maxRetryAfter : retryAfter;
        var retryAfterUntil = _timeProvider.GetUtcNow().Add(actualRetryAfter);
        
        _retryAfterUntil.AddOrUpdate(domain, retryAfterUntil, (_, _) => retryAfterUntil);
        
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

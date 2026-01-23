namespace SamoBot.Infrastructure.Options;

public class CrawlerOptions
{
    public const string SectionName = "Crawler";

    /// <summary>
    /// Default delay between requests to the same domain (in milliseconds)
    /// </summary>
    public int DefaultDelayMs { get; set; } = 1000; // 1 second default

    /// <summary>
    /// Minimum delay between requests to the same domain (in milliseconds)
    /// </summary>
    public int MinDelayMs { get; set; } = 500; // 500ms minimum

    /// <summary>
    /// Maximum delay between requests to the same domain (in milliseconds)
    /// </summary>
    public int MaxDelayMs { get; set; } = 60000; // 60 seconds maximum

    /// <summary>
    /// Whether to respect Retry-After headers from servers
    /// </summary>
    public bool RespectRetryAfter { get; set; } = true;

    /// <summary>
    /// Maximum retry-after delay to respect (in seconds)
    /// </summary>
    public int MaxRetryAfterSeconds { get; set; } = 3600; // 1 hour maximum

    /// <summary>
    /// Initial (median) delay for decorrelated jitter backoff (in milliseconds)
    /// </summary>
    public int InitialBackoffMs { get; set; } = 1000;

    /// <summary>
    /// Maximum number of retries for transient errors
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Status codes that should trigger exponential backoff
    /// </summary>
    public int[] BackoffStatusCodes { get; set; } = [429, 503, 502, 504];

    /// <summary>
    /// Whether to use per-domain rate limiting
    /// </summary>
    public bool UsePerDomainRateLimiting { get; set; } = true;

}

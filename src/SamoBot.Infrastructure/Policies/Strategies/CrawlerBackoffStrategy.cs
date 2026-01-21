using Polly.Contrib.WaitAndRetry;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Policies.Strategies;

public static class CrawlerBackoffStrategy
{
    public static IEnumerable<TimeSpan> CreateBackoffDelays(CrawlerOptions options)
    {
        var initialDelay = TimeSpan.FromMilliseconds(options.InitialBackoffMs);
        return Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: initialDelay,
            retryCount: options.MaxRetries);
    }
}

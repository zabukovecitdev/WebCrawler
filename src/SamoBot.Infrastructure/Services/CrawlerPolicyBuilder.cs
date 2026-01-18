using Microsoft.Extensions.Logging;
using Polly;
using Polly.Contrib.WaitAndRetry;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Services;

public static class CrawlerPolicyBuilder
{
    public static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(
        CrawlerOptions options,
        ILogger logger)
    {
        var initialDelay = TimeSpan.FromMilliseconds(options.InitialBackoffMs);

        return Policy
            .HandleResult<HttpResponseMessage>(response =>
            {
                // Retry on specific status codes
                var statusCode = (int)response.StatusCode;
                return options.BackoffStatusCodes.Contains(statusCode);
            })
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                Backoff.DecorrelatedJitterBackoffV2(
                    medianFirstRetryDelay: initialDelay,
                    retryCount: options.MaxRetries),
                onRetry: (outcome, timespan, retryCount) =>
                {
                    if (outcome.Result != null)
                    {
                        var statusCode = (int)outcome.Result.StatusCode;
                        logger.LogWarning(
                            "Retry {RetryCount}/{MaxRetries} after {Delay}ms for status code {StatusCode}",
                            retryCount, options.MaxRetries, timespan.TotalMilliseconds, statusCode);
                    }
                    else if (outcome.Exception != null)
                    {
                        logger.LogWarning(
                            outcome.Exception,
                            "Retry {RetryCount}/{MaxRetries} after {Delay}ms due to exception",
                            retryCount, options.MaxRetries, timespan.TotalMilliseconds);
                    }
                });
    }
}
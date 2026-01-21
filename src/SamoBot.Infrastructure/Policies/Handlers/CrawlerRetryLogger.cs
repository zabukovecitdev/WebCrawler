using Microsoft.Extensions.Logging;
using Polly;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Policies.Handlers;

public static class CrawlerRetryLogger
{
    public static void LogRetry(
        DelegateResult<HttpResponseMessage> outcome,
        TimeSpan timespan,
        int retryCount,
        CrawlerOptions options,
        ILogger logger)
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
    }
}

using Microsoft.Extensions.Logging;
using Polly;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Policies.Handlers;
using SamoBot.Infrastructure.Policies.Strategies;

namespace SamoBot.Infrastructure.Policies;

public static class CrawlerRetryPolicyBuilder
{
    public static IAsyncPolicy<HttpResponseMessage> BuildRetryPolicy(
        CrawlerOptions options,
        ILogger logger)
    {
        var backoffDelays = CrawlerBackoffStrategy.CreateBackoffDelays(options);

        return Policy
            .HandleResult<HttpResponseMessage>(response =>
                CrawlerRetryHandler.ShouldRetryOnStatusCode(response, options))
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                backoffDelays,
                onRetry: (outcome, timespan, retryCount, context) =>
                    CrawlerRetryLogger.LogRetry(outcome, timespan, retryCount, options, logger));
    }
}

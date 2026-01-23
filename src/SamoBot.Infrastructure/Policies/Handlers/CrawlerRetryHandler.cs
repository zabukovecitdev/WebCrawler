using System.Net;
using SamoBot.Infrastructure.Options;

namespace SamoBot.Infrastructure.Policies.Handlers;

public static class CrawlerRetryHandler
{
    public static bool ShouldRetryOnStatusCode(HttpResponseMessage response, CrawlerOptions options)
    {
        var statusCode = (int)response.StatusCode;
        
        return options.BackoffStatusCodes.Contains(statusCode);
    }
}

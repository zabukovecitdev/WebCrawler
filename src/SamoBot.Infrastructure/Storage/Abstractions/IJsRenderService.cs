using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IJsRenderService
{
    Task<FetchedContent?> RenderPageAsync(string url, int timeoutMs, CancellationToken cancellationToken = default);
}

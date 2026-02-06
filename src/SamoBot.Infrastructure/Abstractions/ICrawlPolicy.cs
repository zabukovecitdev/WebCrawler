using FluentResults;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Abstractions;

public interface ICrawlPolicy
{
    Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default);
}

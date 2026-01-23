using FluentResults;
using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Abstractions;

public interface ICrawlPolicy
{
    Task<Result<UrlContentMetadata>> ExecuteAsync(
        ScheduledUrl scheduledUrl,
        Func<CancellationToken, Task<Result<UrlContentMetadata>>> action,
        CancellationToken cancellationToken = default);
}

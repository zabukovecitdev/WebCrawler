using FluentResults;
using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IContentProcessingPipeline
{
    Task<Result<UrlContentMetadata>> ProcessContent(
        ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default);
}

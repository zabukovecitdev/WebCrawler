using FluentResults;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IContentProcessingPipeline
{
    Task<Result<UrlContentMetadata>> ProcessContent(
        ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default);
}

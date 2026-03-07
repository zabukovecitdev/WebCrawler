using SamoBot.Infrastructure.Data;

namespace SamoBot.Infrastructure.Services.Abstractions;

public interface IIndexerService
{
    Task Index(IEnumerable<ParsedDocumentEntity> documents, CancellationToken cancellationToken = default);
}
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface IParsedDocumentRepository
{
    Task<int> SaveParsedDocument(int urlFetchId, ParsedDocument parsedDocument, CancellationToken cancellationToken = default);
    Task<ParsedDocument?> GetByUrlFetchId(int urlFetchId, CancellationToken cancellationToken = default);
}

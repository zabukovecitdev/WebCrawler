using System.Data;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface IParsedDocumentRepository
{
    Task<int> SaveParsedDocument(int urlFetchId, ParsedDocument parsedDocument, CancellationToken cancellationToken = default);
    Task<ParsedDocument?> GetByUrlFetchId(int urlFetchId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ParsedDocumentEntity>> GetUnindexed(int limit, CancellationToken cancellationToken = default);
    /// <summary>
    /// Locks unindexed documents for this worker using FOR UPDATE SKIP LOCKED and claims them by setting IndexingStartedAt.
    /// Call with an open transaction; commit after to release the lock. Another instance will not see these rows until claim expires or they are marked indexed.
    /// </summary>
    Task<IEnumerable<ParsedDocumentEntity>> GetAndClaimUnindexed(int limit, DateTimeOffset staleClaimBefore, IDbTransaction transaction, CancellationToken cancellationToken = default);
    Task MarkAsIndexed(IEnumerable<int> parsedDocumentIds, CancellationToken cancellationToken = default);
}

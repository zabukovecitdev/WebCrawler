namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IFetchRecordPersistenceService
{
    Task PersistFetchRecordAsync(
        int discoveredUrlId,
        int statusCode,
        string? contentType,
        long? contentLength,
        long? responseTimeMs,
        string? objectName,
        CancellationToken cancellationToken = default);
}

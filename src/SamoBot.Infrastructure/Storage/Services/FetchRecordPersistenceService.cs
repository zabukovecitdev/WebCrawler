using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Data;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class FetchRecordPersistenceService : IFetchRecordPersistenceService
{
    private readonly IUrlFetchRepository _urlFetchRepository;
    private readonly IDiscoveredUrlRepository _discoveredUrlRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FetchRecordPersistenceService> _logger;

    public FetchRecordPersistenceService(
        IUrlFetchRepository urlFetchRepository,
        IDiscoveredUrlRepository discoveredUrlRepository,
        TimeProvider timeProvider,
        ILogger<FetchRecordPersistenceService> logger)
    {
        _urlFetchRepository = urlFetchRepository;
        _discoveredUrlRepository = discoveredUrlRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task PersistFetchRecordAsync(
        int discoveredUrlId,
        int statusCode,
        string? contentType,
        long? contentLength,
        long? responseTimeMs,
        string? objectName,
        CancellationToken cancellationToken = default)
    {
        int? fetchId = await CreateUrlFetchRecordAsync(
            discoveredUrlId,
            statusCode,
            contentType,
            contentLength,
            responseTimeMs,
            objectName,
            cancellationToken);

        await UpdateDiscoveredUrlAsync(
            discoveredUrlId,
            fetchId,
            statusCode,
            contentType,
            contentLength,
            cancellationToken);
    }

    private async Task<int?> CreateUrlFetchRecordAsync(
        int discoveredUrlId,
        int statusCode,
        string? contentType,
        long? contentLength,
        long? responseTimeMs,
        string? objectName,
        CancellationToken cancellationToken)
    {
        try
        {
            var urlFetch = new UrlFetch
            {
                DiscoveredUrlId = discoveredUrlId,
                FetchedAt = _timeProvider.GetUtcNow(),
                StatusCode = statusCode,
                ContentType = contentType,
                ContentLength = contentLength,
                ResponseTimeMs = responseTimeMs,
                ObjectName = objectName
            };

            var insertedId = await _urlFetchRepository.Insert(urlFetch, cancellationToken);
            if (insertedId <= 0)
            {
                _logger.LogWarning("Failed to create UrlFetch record for DiscoveredUrl {Id}", discoveredUrlId);
                return null;
            }

            return insertedId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create UrlFetch record for DiscoveredUrl {Id}", discoveredUrlId);
            return null;
        }
    }

    private async Task UpdateDiscoveredUrlAsync(
        int discoveredUrlId,
        int? fetchId,
        int statusCode,
        string? contentType,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _discoveredUrlRepository.UpdateAfterFetch(
                discoveredUrlId,
                fetchId,
                cancellationToken);

            if (!updated)
            {
                _logger.LogWarning("Failed to update DiscoveredUrl {Id} after fetch", discoveredUrlId);
            }
            else
            {
                _logger.LogInformation(
                    "Updated DiscoveredUrl {Id} after fetch - FetchId: {FetchId}, StatusCode: {StatusCode}, ContentType: {ContentType}, ContentLength: {ContentLength}",
                    discoveredUrlId,
                    fetchId?.ToString() ?? "none",
                    statusCode,
                    contentType,
                    contentLength);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update DiscoveredUrl {Id} after fetch", discoveredUrlId);
        }
    }
}

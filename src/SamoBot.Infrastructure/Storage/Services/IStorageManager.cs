using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Services;

public interface IStorageManager
{
    Task CreateBucket(string bucketName);

    Task<UrlContentMetadata> UploadContent(string url, string bucket, string objectName,
        int? discoveredUrlId = null, CancellationToken cancellationToken = default);
}
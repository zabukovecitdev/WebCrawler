using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IMinioHtmlUploader
{
    Task<UploadResult> Upload(
        string bucket,
        string objectName,
        byte[] contentBytes,
        string? contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads an object from the specified bucket into memory and rewinds the stream to position 0.
    /// </summary>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="objectName">Object key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A memory stream containing the object contents positioned at the beginning.</returns>
    Task<MemoryStream> GetObject(
        string bucket,
        string objectName,
        CancellationToken cancellationToken = default);
}

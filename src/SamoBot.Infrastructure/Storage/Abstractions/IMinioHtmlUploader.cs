using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IMinioHtmlUploader
{
    Task<UploadResult> Upload(
        string bucket,
        string objectName,
        byte[] contentBytes,
        string? contentType,
        CancellationToken cancellationToken = default);
}

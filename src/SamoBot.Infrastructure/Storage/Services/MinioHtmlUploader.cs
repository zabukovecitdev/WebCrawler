using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioHtmlUploader : IMinioHtmlUploader
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioHtmlUploader> _logger;

    public MinioHtmlUploader(IMinioClient minioClient, ILogger<MinioHtmlUploader> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    public async Task<UploadResult> Upload(
        string bucket,
        string objectName,
        byte[] contentBytes,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? MediaTypeNames.Text.Html
            : contentType;
        var resolvedObjectName = EnsureHtmlExtension(objectName);

        try
        {
            using var uploadStream = new MemoryStream(contentBytes, writable: false);
            var putArgs = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(resolvedObjectName)
                .WithStreamData(uploadStream)
                .WithObjectSize(contentBytes.LongLength)
                .WithContentType(resolvedContentType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);

            _logger.LogInformation(
                "Uploaded HTML to {Bucket}/{ObjectName} (Size: {Size} bytes)",
                bucket,
                resolvedObjectName,
                contentBytes.LongLength);

            return new UploadResult { ObjectName = resolvedObjectName };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload HTML to {Bucket}/{ObjectName}", bucket, resolvedObjectName);
            return new UploadResult
            {
                Error = $"Failed to upload HTML to {bucket}/{resolvedObjectName}: {ex.Message}"
            };
        }
    }

    private static string EnsureHtmlExtension(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return "index.html";
        }

        var extension = Path.GetExtension(objectName);
        if (string.IsNullOrEmpty(extension))
        {
            return $"{objectName}.html";
        }

        if (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
        {
            return objectName;
        }

        return objectName[..^extension.Length] + ".html";
    }
}

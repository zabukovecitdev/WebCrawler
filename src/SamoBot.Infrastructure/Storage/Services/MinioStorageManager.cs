using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioStorageManager(IMinioClient minioClient, ILogger<MinioStorageManager> logger, IHttpClientFactory httpClientFactory)
    : IStorageManager
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("crawl");

    public async Task CreateBucket(string bucketName)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
            
            var found = await minioClient.BucketExistsAsync(bucketExistsArgs);
            if (found)
            {
                logger.LogInformation("Bucket {BucketName} already exists", bucketName);
            }
            else
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                
                await minioClient.MakeBucketAsync(makeBucketArgs);
                logger.LogInformation("Bucket {BucketName} created successfully", bucketName);
            }
        }
        catch (MinioException e)
        {
            logger.LogError(e, "Error occurred while creating bucket {BucketName}", bucketName);
            throw;
        }
    }
    
    public async Task<UrlContentMetadata> UploadContent(string url, string bucket, string objectName,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        var statusCode = (int)response.StatusCode;

        var responseContentType = response.Content.Headers.ContentType?.MediaType ?? MediaTypeNames.Text.Html;
        var contentLength = response.Content.Headers.ContentLength ?? -1;

        if (response.IsSuccessStatusCode)
        {
            await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            var uploadStream = httpStream;
            MemoryStream? memoryStream = null;
            
            if (contentLength < 0)
            {
                memoryStream = new MemoryStream();
                await httpStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Position = 0;
                contentLength = memoryStream.Length;
                uploadStream = memoryStream;
            }

            try
            {
                var putArgs = new PutObjectArgs()
                    .WithBucket(bucket)
                    .WithObject(objectName)
                    .WithStreamData(uploadStream)
                    .WithContentType(responseContentType)
                    .WithObjectSize(contentLength);

                await minioClient.PutObjectAsync(putArgs, cancellationToken);
                logger.LogInformation("Successfully uploaded content from {Url} to {Bucket}/{ObjectName} (Size: {Size} bytes)", 
                    url, bucket, objectName, contentLength);
            }
            finally
            {
                if (memoryStream != null)
                {
                    await memoryStream.DisposeAsync();
                }
            }
        }
        else
        {
            logger.LogWarning("Skipping upload for {Url} due to non-success status code: {StatusCode}", url, statusCode);
        }

        return new UrlContentMetadata
        {
            ContentType = responseContentType,
            ContentLength = contentLength,
            StatusCode = statusCode
        };
    }
}
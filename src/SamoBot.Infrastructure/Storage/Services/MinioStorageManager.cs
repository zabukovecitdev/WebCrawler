using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Polly;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services;

namespace SamoBot.Infrastructure.Storage.Services;

public class MinioStorageManager : IStorageManager
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageManager> _logger;
    private readonly HttpClient _http;
    private readonly IDomainRateLimiter _rateLimiter;
    private readonly CrawlerOptions _crawlerOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public MinioStorageManager(
        IMinioClient minioClient,
        ILogger<MinioStorageManager> logger,
        IHttpClientFactory httpClientFactory,
        IDomainRateLimiter rateLimiter,
        IOptions<CrawlerOptions> crawlerOptions,
        TimeProvider timeProvider)
    {
        _minioClient = minioClient;
        _logger = logger;
        _http = httpClientFactory.CreateClient("crawl");
        _rateLimiter = rateLimiter;
        _crawlerOptions = crawlerOptions.Value;
        _timeProvider = timeProvider;
        _retryPolicy = CrawlerPolicyBuilder.BuildRetryPolicy(_crawlerOptions, _logger);
    }

    public async Task CreateBucket(string bucketName)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);
            
            var found = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            if (found)
            {
                _logger.LogInformation("Bucket {BucketName} already exists", bucketName);
            }
            else
            {
                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);
                
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Bucket {BucketName} created successfully", bucketName);
            }
        }
        catch (MinioException e)
        {
            _logger.LogError(e, "Error occurred while creating bucket {BucketName}", bucketName);
            throw;
        }
    }
    
    public async Task<UrlContentMetadata> UploadContent(string url, string bucket, string objectName,
        CancellationToken cancellationToken = default)
    {
        // Wait for rate limiting before making the request
        await _rateLimiter.WaitForDomainDelayAsync(url, cancellationToken);

        // Execute request with Polly retry policy
        HttpResponseMessage? response;
        try
        {
            response = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var httpResponse = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                return httpResponse;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url} after all retries", url);
            throw;
        }

        if (response == null)
        {
            throw new InvalidOperationException($"Failed to get response for {url}");
        }

        _rateLimiter.RecordRequest(url);

        try
        {
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

                    await _minioClient.PutObjectAsync(putArgs, cancellationToken);
                    _logger.LogInformation("Successfully uploaded content from {Url} to {Bucket}/{ObjectName} (Size: {Size} bytes)", 
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
                _logger.LogWarning("Skipping upload for {Url} due to non-success status code: {StatusCode}", url, statusCode);
            }

            return new UrlContentMetadata
            {
                ContentType = responseContentType,
                ContentLength = contentLength,
                StatusCode = statusCode
            };
        }
        finally
        {
            response.Dispose();
        }
    }
}
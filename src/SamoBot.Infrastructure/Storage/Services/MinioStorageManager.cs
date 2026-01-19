using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Polly;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services;

namespace SamoBot.Infrastructure.Storage.Services;

internal class ContentUploadContext
{
    public string Url { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string ObjectName { get; init; } = string.Empty;
    public int? DiscoveredUrlId { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public HttpResponseMessage? Response { get; set; }
    public string ContentType { get; set; } = MediaTypeNames.Text.Html;
    public long ContentLength { get; set; } = -1;
    public int StatusCode { get; set; }
    public Stream? UploadStream { get; set; }
    public MemoryStream? MemoryStream { get; set; }
}

internal class ContentUploadBuilder
{
    private readonly ContentUploadContext _context;
    private readonly IDomainRateLimiter _rateLimiter;
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly IMinioClient _minioClient;
    private readonly IDiscoveredUrlRepository? _repository;

    public ContentUploadBuilder(
        ContentUploadContext context,
        IDomainRateLimiter rateLimiter,
        HttpClient httpClient,
        IAsyncPolicy<HttpResponseMessage> retryPolicy,
        TimeProvider timeProvider,
        ILogger logger,
        IMinioClient minioClient,
        IDiscoveredUrlRepository? repository = null)
    {
        _context = context;
        _rateLimiter = rateLimiter;
        _httpClient = httpClient;
        _retryPolicy = retryPolicy;
        _timeProvider = timeProvider;
        _logger = logger;
        _minioClient = minioClient;
        _repository = repository;
    }

    public async Task<ContentUploadBuilder> WaitForRateLimitAsync()
    {
        await _rateLimiter.WaitForDomainDelayAsync(_context.Url, _context.CancellationToken);
        return this;
    }

    public async Task<ContentUploadBuilder> FetchContentAsync()
    {
        try
        {
            _context.Response = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, _context.Url);
                var httpResponse = await _httpClient.SendAsync(
                    request, 
                    HttpCompletionOption.ResponseHeadersRead, 
                    _context.CancellationToken);
                
                await ProcessRetryAfterHeaderAsync(httpResponse);
                
                return httpResponse;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url} after all retries", _context.Url);
            throw;
        }

        if (_context.Response == null)
        {
            throw new InvalidOperationException($"Failed to get response for {_context.Url}");
        }

        await _rateLimiter.RecordRequestAsync(_context.Url);
        return this;
    }

    public ContentUploadBuilder ExtractMetadata()
    {
        if (_context.Response == null)
        {
            throw new InvalidOperationException("Response is null. Call FetchContentAsync first.");
        }

        _context.StatusCode = (int)_context.Response.StatusCode;
        _context.ContentType = _context.Response.Content.Headers.ContentType?.MediaType ?? MediaTypeNames.Text.Html;
        _context.ContentLength = _context.Response.Content.Headers.ContentLength ?? -1;
        
        return this;
    }

    public async Task<ContentUploadBuilder> PrepareStream()
    {
        if (_context.Response == null)
        {
            throw new InvalidOperationException("Response is null. Call FetchContentAsync first.");
        }

        _context.UploadStream = await _context.Response.Content.ReadAsStreamAsync(_context.CancellationToken);

        if (_context.ContentLength < 0)
        {
            _context.MemoryStream = new MemoryStream();
            await _context.UploadStream.CopyToAsync(_context.MemoryStream, _context.CancellationToken);
            _context.MemoryStream.Position = 0;
            _context.ContentLength = _context.MemoryStream.Length;
            _context.UploadStream = _context.MemoryStream;
        }

        return this;
    }

    public async Task<ContentUploadBuilder> UploadToMinio()
    {
        if (_context.UploadStream == null)
        {
            throw new InvalidOperationException("UploadStream is null. Call PrepareStream first.");
        }

        if (!_context.Response!.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Skipping upload for {Url} due to non-success status code: {StatusCode}",
                _context.Url, _context.StatusCode);
            return this;
        }

        try
        {
            var putArgs = new PutObjectArgs()
                .WithBucket(_context.Bucket)
                .WithObject(_context.ObjectName)
                .WithStreamData(_context.UploadStream)
                .WithContentType(_context.ContentType)
                .WithObjectSize(_context.ContentLength);

            await _minioClient.PutObjectAsync(putArgs, _context.CancellationToken);
            _logger.LogInformation(
                "Successfully uploaded content from {Url} to {Bucket}/{ObjectName} (Size: {Size} bytes)",
                _context.Url, _context.Bucket, _context.ObjectName, _context.ContentLength);
        }
        finally
        {
            if (_context.MemoryStream != null)
            {
                await _context.MemoryStream.DisposeAsync();
            }
        }

        return this;
    }

    public async Task<ContentUploadBuilder> UpdateDiscoveredUrl()
    {
        if (_context.DiscoveredUrlId.HasValue && _repository != null)
        {
            var metadata = new UrlContentMetadata
            {
                ContentType = _context.ContentType,
                ContentLength = _context.ContentLength,
                StatusCode = _context.StatusCode
            };

            var updated = await _repository.UpdateDiscoveredUrlWithMetadata(
                _context.DiscoveredUrlId.Value,
                metadata,
                _context.ObjectName,
                _context.CancellationToken);

            if (!updated)
            {
                _logger.LogWarning("Failed to update DiscoveredUrl {Id} with metadata", _context.DiscoveredUrlId.Value);
            }
            else
            {
                _logger.LogInformation(
                    "Updated DiscoveredUrl {Id} with metadata - StatusCode: {StatusCode}, ContentType: {ContentType}, ContentLength: {ContentLength}",
                    _context.DiscoveredUrlId.Value, metadata.StatusCode, metadata.ContentType, metadata.ContentLength);
            }
        }

        return this;
    }

    public UrlContentMetadata BuildMetadata()
    {
        return new UrlContentMetadata
        {
            ContentType = _context.ContentType,
            ContentLength = _context.ContentLength,
            StatusCode = _context.StatusCode
        };
    }

    public void Dispose()
    {
        _context.Response?.Dispose();
        _context.MemoryStream?.Dispose();
    }

    private async Task ProcessRetryAfterHeaderAsync(HttpResponseMessage httpResponse)
    {
        if (httpResponse.Headers.RetryAfter == null)
        {
            return;
        }

        var retryAfter = httpResponse.Headers.RetryAfter.Delta ??
                        (httpResponse.Headers.RetryAfter.Date.HasValue
                            ? httpResponse.Headers.RetryAfter.Date.Value - _timeProvider.GetUtcNow()
                            : TimeSpan.Zero);
        
        if (retryAfter > TimeSpan.Zero)
        {
            await _rateLimiter.RecordRetryAfterAsync(_context.Url, retryAfter);
        }
    }
}

public class MinioStorageManager : IStorageManager
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageManager> _logger;
    private readonly HttpClient _http;
    private readonly IDomainRateLimiter _rateLimiter;
    private readonly CrawlerOptions _crawlerOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly IDiscoveredUrlRepository? _repository;

    public MinioStorageManager(
        IMinioClient minioClient,
        ILogger<MinioStorageManager> logger,
        IHttpClientFactory httpClientFactory,
        IDomainRateLimiter rateLimiter,
        IOptions<CrawlerOptions> crawlerOptions,
        TimeProvider timeProvider,
        IDiscoveredUrlRepository? repository = null)
    {
        _minioClient = minioClient;
        _logger = logger;
        _http = httpClientFactory.CreateClient("crawl");
        _rateLimiter = rateLimiter;
        _crawlerOptions = crawlerOptions.Value;
        _timeProvider = timeProvider;
        _retryPolicy = CrawlerPolicyBuilder.BuildRetryPolicy(_crawlerOptions, _logger);
        _repository = repository;
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
    
    public async Task<UrlContentMetadata>  UploadContent(string url, string bucket, string objectName,
        int? discoveredUrlId = null, CancellationToken cancellationToken = default)
    {
        var context = new ContentUploadContext
        {
            Url = url,
            Bucket = bucket,
            ObjectName = objectName,
            DiscoveredUrlId = discoveredUrlId,
            CancellationToken = cancellationToken
        };

        var builder = new ContentUploadBuilder(
            context,
            _rateLimiter,
            _http,
            _retryPolicy,
            _timeProvider,
            _logger,
            _minioClient,
            _repository);

        try
        {
            builder = await builder.WaitForRateLimitAsync();
            builder = await builder.FetchContentAsync();
            builder = builder.ExtractMetadata();
            builder = await builder.PrepareStream();
            builder = await builder.UploadToMinio();
            builder = await builder.UpdateDiscoveredUrl();
            
            return builder.BuildMetadata();
        }
        finally
        {
            builder.Dispose();
        }
    }
}
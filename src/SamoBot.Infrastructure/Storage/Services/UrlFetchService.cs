using Microsoft.Extensions.Logging;
using Polly;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class UrlFetchService : IUrlFetchService
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ILogger<UrlFetchService> _logger;

    public UrlFetchService(
        IHttpClientFactory httpClientFactory,
        IAsyncPolicy<HttpResponseMessage> retryPolicy,
        ILogger<UrlFetchService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("crawl");
        _retryPolicy = retryPolicy;
        _logger = logger;
    }

    public async Task<FetchedContent> Fetch(string url, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? response = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            response = await _retryPolicy.ExecuteAsync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                return await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch {Url}", url);
            return new FetchedContent
            {
                StatusCode = 0,
                Error = $"Failed to fetch {url}: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        if (response == null)
        {
            return new FetchedContent
            {
                StatusCode = 0,
                Error = $"Failed to fetch {url}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }

        var statusCode = (int)response.StatusCode;
        var contentType = response.Content.Headers.ContentType?.ToString();

        try
        {
            var contentBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentLength = contentBytes.LongLength;

            if (contentBytes.Length == 0)
            {
                _logger.LogInformation("Skipping upload for {Url} because response is empty", url);
                return new FetchedContent
                {
                    StatusCode = statusCode,
                    ContentType = contentType,
                    ContentBytes = contentBytes,
                    ContentLength = contentLength,
                    Error = $"Empty response content for {url}",
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            return new FetchedContent
            {
                StatusCode = statusCode,
                ContentType = contentType,
                ContentBytes = contentBytes,
                ContentLength = contentLength,
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read content from {Url}", url);
            return new FetchedContent
            {
                StatusCode = statusCode,
                ContentType = contentType,
                Error = $"Failed to read content from {url}: {ex.Message}",
                ResponseTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            stopwatch.Stop();
            response.Dispose();
        }
    }
}

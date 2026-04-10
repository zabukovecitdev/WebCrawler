using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services.Abstractions;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class ContentProcessingPipeline : IContentProcessingPipeline
{
    private readonly IUrlFetchService _fetchService;
    private readonly IJsRenderService _jsRenderService;
    private readonly IHtmlContentValidator _htmlContentValidator;
    private readonly IMinioHtmlUploader _htmlUploader;
    private readonly IFetchRecordPersistenceService _persistenceService;
    private readonly IObjectNameGenerator _objectNameGenerator;
    private readonly MinioOptions _minioOptions;
    private readonly ChromeRenderingOptions _chromeOptions;
    private readonly ILogger<ContentProcessingPipeline> _logger;
    private readonly ICrawlPolicy _crawlPolicy;
    private readonly ICrawlTelemetryService _telemetry;

    public ContentProcessingPipeline(
        IUrlFetchService fetchService,
        IJsRenderService jsRenderService,
        IHtmlContentValidator htmlContentValidator,
        IMinioHtmlUploader htmlUploader,
        IFetchRecordPersistenceService persistenceService,
        IObjectNameGenerator objectNameGenerator,
        IOptions<MinioOptions> minioOptions,
        IOptions<ChromeRenderingOptions> chromeOptions,
        ILogger<ContentProcessingPipeline> logger,
        ICrawlPolicy crawlPolicy,
        ICrawlTelemetryService telemetry)
    {
        _fetchService = fetchService;
        _jsRenderService = jsRenderService;
        _htmlContentValidator = htmlContentValidator;
        _htmlUploader = htmlUploader;
        _persistenceService = persistenceService;
        _objectNameGenerator = objectNameGenerator;
        _minioOptions = minioOptions.Value;
        _chromeOptions = chromeOptions.Value;
        _logger = logger;
        _crawlPolicy = crawlPolicy;
        _telemetry = telemetry;
    }

    public async Task<Result<UrlContentMetadata>> ProcessContent(ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;
        
        return await _crawlPolicy.ExecuteAsync(
            scheduledUrl,
            async ct =>
            {
                await _telemetry.PublishAsync(scheduledUrl.CrawlJobId, "FetchStarted",
                    new { url, discoveredUrlId = scheduledUrl.Id, depth = scheduledUrl.Depth }, ct);

                var fetchResult = await FetchContentAsync(scheduledUrl, ct);
                await _telemetry.PublishAsync(scheduledUrl.CrawlJobId, "FetchCompleted",
                    new
                    {
                        url,
                        discoveredUrlId = scheduledUrl.Id,
                        statusCode = fetchResult.StatusCode,
                        contentLength = fetchResult.ContentLength,
                        rendered = scheduledUrl.UseJsRendering && _chromeOptions.Enabled
                    }, ct);

                var objectName = await UploadHtmlAsync(url, fetchResult, ct);

                if (scheduledUrl.Id > 0)
                {
                    await _persistenceService.PersistFetchRecordAsync(
                        scheduledUrl.Id,
                        fetchResult.StatusCode,
                        fetchResult.ContentType,
                        fetchResult.ContentLength,
                        fetchResult.ResponseTimeMs,
                        objectName,
                        ct);
                }
                else
                {
                    _logger.LogDebug("Skipping fetch record persistence for URL {Url} because DiscoveredUrlId is missing", url);
                }

                return Result.Ok(new UrlContentMetadata
                {
                    ContentType = fetchResult.ContentType,
                    ContentLength = fetchResult.ContentLength,
                    StatusCode = fetchResult.StatusCode,
                    WasDeferred = false
                });
            },
            cancellationToken);
    }

    private async Task<FetchedContent> FetchContentAsync(ScheduledUrl scheduledUrl, CancellationToken cancellationToken)
    {
        var url = scheduledUrl.Url;
        if (scheduledUrl.UseJsRendering && _chromeOptions.Enabled && !string.IsNullOrWhiteSpace(_chromeOptions.CdpEndpoint))
        {
            var rendered = await _jsRenderService.RenderPageAsync(url, _chromeOptions.DefaultTimeoutMs, cancellationToken);
            if (rendered != null)
            {
                return rendered;
            }

            _logger.LogInformation("JS render returned null for {Url}, falling back to HTTP fetch", url);
        }

        return await _fetchService.Fetch(url, cancellationToken);
    }

    private async Task<string?> UploadHtmlAsync(string url, FetchedContent fetchResult, CancellationToken cancellationToken)
    {
        if (fetchResult.ContentBytes is not { Length: > 0 } contentBytes)
        {
            return null;
        }

        if (!_htmlContentValidator.IsHtml(fetchResult.ContentType, contentBytes))
        {
            return null;
        }

        var generatedObjectName = _objectNameGenerator.GenerateHierarchical(url, fetchResult.ContentType);
        var uploadResult = await _htmlUploader.Upload(
            _minioOptions.BucketName,
            generatedObjectName,
            contentBytes,
            fetchResult.ContentType,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(uploadResult.Error))
        {
            _logger.LogWarning("Failed to upload HTML for {Url}: {Error}", url, uploadResult.Error);
            return null;
        }

        return uploadResult.ObjectName;
    }
}

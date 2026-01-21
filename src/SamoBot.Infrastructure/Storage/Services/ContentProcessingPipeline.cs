using FluentResults;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public class ContentProcessingPipeline : IContentProcessingPipeline
{
    private readonly IUrlFetchService _fetchService;
    private readonly IHtmlContentValidator _htmlContentValidator;
    private readonly IMinioHtmlUploader _htmlUploader;
    private readonly IFetchRecordPersistenceService _persistenceService;
    private readonly IObjectNameGenerator _objectNameGenerator;
    private readonly MinioOptions _minioOptions;
    private readonly ILogger<ContentProcessingPipeline> _logger;

    public ContentProcessingPipeline(
        IUrlFetchService fetchService,
        IHtmlContentValidator htmlContentValidator,
        IMinioHtmlUploader htmlUploader,
        IFetchRecordPersistenceService persistenceService,
        IObjectNameGenerator objectNameGenerator,
        IOptions<MinioOptions> minioOptions,
        ILogger<ContentProcessingPipeline> logger)
    {
        _fetchService = fetchService;
        _htmlContentValidator = htmlContentValidator;
        _htmlUploader = htmlUploader;
        _persistenceService = persistenceService;
        _objectNameGenerator = objectNameGenerator;
        _minioOptions = minioOptions.Value;
        _logger = logger;
    }

    public async Task<Result<UrlContentMetadata>> ProcessContentAsync(
        ScheduledUrl scheduledUrl,
        CancellationToken cancellationToken = default)
    {
        var url = scheduledUrl.Url;
        var bucket = _minioOptions.BucketName;
        var fetchResult = await _fetchService.Fetch(url, cancellationToken);

        if (fetchResult.Error != null)
        {
            await PersistFailureIfNeededAsync(scheduledUrl.Id, fetchResult, cancellationToken);
            return Result.Fail(fetchResult.Error);
        }

        if (fetchResult.ContentBytes == null || fetchResult.ContentBytes.Length == 0)
        {
            var error = $"Empty response content for {url}";
            await PersistFailureIfNeededAsync(scheduledUrl.Id, fetchResult, cancellationToken);
            
            return Result.Fail(error);
        }

        if (!_htmlContentValidator.IsHtml(fetchResult.ContentType, fetchResult.ContentBytes))
        {
            _logger.LogInformation(
                "Skipping upload for {Url} because content is not HTML (ContentType: {ContentType})",
                url,
                fetchResult.ContentType ?? "unknown");
            
            var error = $"Content is not HTML for {url}";
            
            await PersistFailureIfNeededAsync(scheduledUrl.Id, fetchResult, cancellationToken);
            
            return Result.Fail(error);
        }

        var objectName = _objectNameGenerator.GenerateHierarchical(url, fetchResult.ContentType);
        var uploadResult = await _htmlUploader.Upload(
            bucket,
            objectName,
            fetchResult.ContentBytes,
            fetchResult.ContentType,
            cancellationToken);

        if (uploadResult.Error != null)
        {
            await PersistFailureIfNeededAsync(scheduledUrl.Id, fetchResult, cancellationToken);
            
            return Result.Fail(uploadResult.Error);
        }

        await PersistSuccessIfNeededAsync(
            scheduledUrl.Id,
            fetchResult,
            uploadResult.ObjectName,
            cancellationToken);

        return Result.Ok(new UrlContentMetadata
        {
            ContentType = fetchResult.ContentType ?? string.Empty,
            ContentLength = fetchResult.ContentLength ?? -1,
            StatusCode = fetchResult.StatusCode
        });
    }

    private async Task PersistFailureIfNeededAsync(
        int discoveredUrlId,
        FetchedContent fetchResult,
        CancellationToken cancellationToken)
    {
        await _persistenceService.PersistFetchRecordAsync(
            discoveredUrlId,
            fetchResult.StatusCode,
            fetchResult.ContentType,
            fetchResult.ContentLength,
            objectName: null,
            cancellationToken);
    }

    private async Task PersistSuccessIfNeededAsync(
        int discoveredUrlId,
        FetchedContent fetchResult,
        string? objectName,
        CancellationToken cancellationToken)
    {
        await _persistenceService.PersistFetchRecordAsync(
            discoveredUrlId,
            fetchResult.StatusCode,
            fetchResult.ContentType,
            fetchResult.ContentLength,
            objectName,
            cancellationToken);
    }
}

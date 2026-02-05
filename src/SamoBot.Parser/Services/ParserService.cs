using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Abstractions;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Storage.Abstractions;
using SamoBot.Infrastructure.Validators;

namespace SamoBot.Parser.Services;

public class ParserService : IParserService
{
    private readonly ILogger<ParserService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MinioOptions _minioOptions;
    private readonly IMinioHtmlUploader _htmlUploader;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IDiscoveredUrlPublisher _discoveredUrlPublisher;
    private readonly IMetaRobotsValidator _metaRobotsValidator;
    private const int BatchSize = 10;

    public ParserService(
        ILogger<ParserService> logger,
        IServiceProvider serviceProvider,
        IOptions<MinioOptions> minioOptions,
        IMinioHtmlUploader htmlUploader,
        IDbConnectionFactory connectionFactory,
        IDiscoveredUrlPublisher discoveredUrlPublisher,
        IMetaRobotsValidator metaRobotsValidator)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _minioOptions = minioOptions.Value;
        _htmlUploader = htmlUploader;
        _connectionFactory = connectionFactory;
        _discoveredUrlPublisher = discoveredUrlPublisher;
        _metaRobotsValidator = metaRobotsValidator;
    }

    public async Task ProcessUnparsedFetches(CancellationToken cancellationToken)
    {
        await using var connection = (NpgsqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var urlFetchRepository = scope.ServiceProvider.GetRequiredService<IUrlFetchRepository>();
            var discoveredUrlRepository = scope.ServiceProvider.GetRequiredService<IDiscoveredUrlRepository>();
            var parsedDocumentRepository = scope.ServiceProvider.GetRequiredService<IParsedDocumentRepository>();
            var htmlParser = scope.ServiceProvider.GetRequiredService<IHtmlParser>();

            var unparsedFetches = await urlFetchRepository.GetUnparsedHtmlFetches(BatchSize, transaction, cancellationToken);
            var fetchList = unparsedFetches.ToList();

            if (fetchList.Count == 0)
            {
                _logger.LogDebug("No unparsed HTML fetches found");
                await transaction.CommitAsync(cancellationToken);

                return;
            }

            _logger.LogInformation("Found {Count} unparsed HTML fetches to process", fetchList.Count);

            foreach (var fetch in fetchList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await ProcessFetch(fetch, htmlParser, urlFetchRepository, discoveredUrlRepository, parsedDocumentRepository, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing fetch {FetchId} with object {ObjectName}", 
                        fetch.Id, fetch.ObjectName);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ProcessUnparsedFetches, rolling back transaction");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ProcessFetch(UrlFetch fetch,
        IHtmlParser htmlParser,
        IUrlFetchRepository urlFetchRepository,
        IDiscoveredUrlRepository discoveredUrlRepository,
        IParsedDocumentRepository parsedDocumentRepository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fetch.ObjectName))
        {
            _logger.LogWarning("Fetch {FetchId} has no ObjectName, skipping", fetch.Id);
            return;
        }

        _logger.LogDebug("Processing fetch {FetchId} with object {ObjectName}", fetch.Id, fetch.ObjectName);

        MemoryStream? memoryStream = null;
        try
        {
            // Get the source URL for resolving relative links
            var discoveredUrl = await discoveredUrlRepository.GetById(fetch.DiscoveredUrlId, cancellationToken);
            if (discoveredUrl == null)
            {
                _logger.LogWarning("DiscoveredUrl {DiscoveredUrlId} not found for fetch {FetchId}, skipping", 
                    fetch.DiscoveredUrlId, fetch.Id);
                return;
            }

            var sourceUrl = discoveredUrl.NormalizedUrl ?? discoveredUrl.Url;
            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                _logger.LogWarning("Source URL is empty for fetch {FetchId}, skipping", fetch.Id);
                return;
            }

            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
            {
                _logger.LogWarning("Invalid source URL {SourceUrl} for fetch {FetchId}, skipping", sourceUrl, fetch.Id);
                return;
            }

            memoryStream = await _htmlUploader.GetObject(_minioOptions.BucketName, fetch.ObjectName, cancellationToken);

            var parsedDocument = htmlParser.Parse(memoryStream);

            _logger.LogInformation("Parsed fetch {FetchId} from {ObjectName}: found {LinkCount} links, {ImageCount} images, {HeadingCount} headings",
                fetch.Id, fetch.ObjectName, parsedDocument.Links.Count, parsedDocument.Images.Count, parsedDocument.Headings.Count);


            var shouldFollowLinks = _metaRobotsValidator.ShouldFollowLinks(parsedDocument.RobotsDirectives);

            if (shouldFollowLinks)
            {
                var discoveredUrls = ExtractAbsoluteUrls(parsedDocument.Links, sourceUri).ToList();
                if (discoveredUrls.Count != 0)
                {
                    await _discoveredUrlPublisher.PublishUrlsAsync(discoveredUrls, cancellationToken);
                    
                    _logger.LogInformation("Published {Count} discovered URLs from fetch {FetchId}", discoveredUrls.Count(), fetch.Id);
                }
            }
            else
            {
                _logger.LogInformation("Skipping link extraction for fetch {FetchId} due to nofollow directive", fetch.Id);
            }

            await parsedDocumentRepository.SaveParsedDocument(fetch.Id, parsedDocument, cancellationToken);

            await urlFetchRepository.MarkAsParsed(fetch.Id, cancellationToken);
            
            _logger.LogDebug("Saved parsed document and marked fetch {FetchId} as parsed", fetch.Id);
        }
        catch (Minio.Exceptions.ObjectNotFoundException ex)
        {
            _logger.LogWarning(ex, "Object {ObjectName} not found in Minio for fetch {FetchId}, marking as parsed anyway",
                fetch.ObjectName, fetch.Id);
            
            await urlFetchRepository.MarkAsParsed(fetch.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error processing fetch {FetchId} from object {ObjectName}",
                fetch.Id, fetch.ObjectName);
            throw;
        }
        finally
        {
            if (memoryStream != null) await memoryStream.DisposeAsync();
        }
    }

    private static HashSet<string> ExtractAbsoluteUrls(IEnumerable<ParsedLink> links, Uri sourceUri)
    {
        var absoluteUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            if (string.IsNullOrWhiteSpace(link.Url))
            {
                continue;
            }

            if (link.Url.Contains(':', StringComparison.OrdinalIgnoreCase) && 
                !link.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !link.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (Uri.TryCreate(link.Url, UriKind.Absolute, out var absoluteUri) || Uri.TryCreate(sourceUri, link.Url, out absoluteUri))
                {
                    if (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps)
                    {
                        absoluteUrls.Add(absoluteUri.AbsoluteUri);
                    }
                }
            }
            catch
            {
                // Skip invalid URLs
            }
        }

        return absoluteUrls;
    }
}

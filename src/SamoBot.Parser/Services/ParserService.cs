using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Parser.Services;

public class ParserService : IParserService
{
    private readonly ILogger<ParserService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly MinioOptions _minioOptions;
    private readonly IMinioHtmlUploader _htmlUploader;
    private readonly IDbConnectionFactory _connectionFactory;
    private const int BatchSize = 10;

    public ParserService(
        ILogger<ParserService> logger,
        IServiceProvider serviceProvider,
        IOptions<MinioOptions> minioOptions,
        IMinioHtmlUploader htmlUploader,
        IDbConnectionFactory connectionFactory)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _minioOptions = minioOptions.Value;
        _htmlUploader = htmlUploader;
        _connectionFactory = connectionFactory;
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
                    await ProcessFetch(fetch, htmlParser, urlFetchRepository, cancellationToken);
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
            memoryStream = await _htmlUploader.GetObject(
                _minioOptions.BucketName,
                fetch.ObjectName,
                cancellationToken);

            var links = await htmlParser.Parse(memoryStream, cancellationToken);

            _logger.LogInformation("Parsed fetch {FetchId} from {ObjectName}: found {LinkCount} links",
                fetch.Id, fetch.ObjectName, links.Links.Count);

            await urlFetchRepository.MarkAsParsed(fetch.Id, cancellationToken);
            
            _logger.LogDebug("Marked fetch {FetchId} as parsed", fetch.Id);
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
}

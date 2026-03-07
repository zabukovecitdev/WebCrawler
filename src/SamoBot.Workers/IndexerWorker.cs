using System.Data;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Workers;

public class IndexerWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan StaleClaimThreshold = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<IndexerWorker> _logger;

    public IndexerWorker(
        IServiceProvider serviceProvider,
        IDbConnectionFactory connectionFactory,
        ILogger<IndexerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Indexer worker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimed = await ClaimAndIndexBatch(stoppingToken);
                if (claimed == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in indexer worker");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }

        _logger.LogInformation("Indexer worker stopping...");
    }

    private async Task<int> ClaimAndIndexBatch(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await ((DbConnection)connection).OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        List<ParsedDocumentEntity> documents;
        using (var scope = _serviceProvider.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IParsedDocumentRepository>();
            var staleBefore = DateTimeOffset.UtcNow - StaleClaimThreshold;
            documents = (await repository.GetAndClaimUnindexed(BatchSize, staleBefore, transaction, cancellationToken)).ToList();
            if (documents.Count == 0)
            {
                transaction.Commit();
                
                return 0;
            }
            transaction.Commit();
        }

        using (var scope = _serviceProvider.CreateScope())
        {
            var indexer = scope.ServiceProvider.GetRequiredService<IIndexerService>();
            await indexer.Index(documents, cancellationToken);
        }

        return documents.Count;
    }
}
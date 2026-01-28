using System.Data;
using Microsoft.Extensions.Logging;
using Npgsql;
using Samobot.Domain.Enums;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Database;

namespace SamoBot.Scheduler.Services;

public class SchedulerService : ISchedulerService
{
    private readonly IDiscoveredUrlRepository _repository;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SchedulerService> _logger;

    public SchedulerService(
        IDiscoveredUrlRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<SchedulerService> logger)
    {
        _repository = repository;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<DiscoveredUrl>> GetScheduledEntities(int limit, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching URLs ready for crawling with limit {Limit}", limit);
        
        await using var connection = (NpgsqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var urls = await _repository.GetReadyForCrawling(limit, transaction, cancellationToken);
            var urlList = urls.ToList();
            
            if (urlList.Count > 0)
            {
                _logger.LogDebug("Found {Count} URLs ready for crawling, updating status to InFlight", urlList.Count);
                
                var ids = urlList.Select(u => u.Id).ToList();
                await _repository.UpdateStatus(ids, UrlStatus.InFlight, transaction, cancellationToken);
                
                await transaction.CommitAsync(cancellationToken);
                
                _logger.LogInformation("Successfully updated {Count} URLs to InFlight status", urlList.Count);
            }
            else
            {
                _logger.LogDebug("No URLs ready for crawling");
                
                await transaction.CommitAsync(cancellationToken);
            }
            
            return urlList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing URLs ready for crawling, rolling back transaction");
            await transaction.RollbackAsync(cancellationToken);
            
            throw;
        }
    }
}

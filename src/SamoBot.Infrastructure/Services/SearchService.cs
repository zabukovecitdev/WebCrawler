using Meilisearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Infrastructure.Services;

public class SearchService : ISearchService
{
    private readonly MeilisearchClient _meilisearchClient;
    private readonly MeilisearchOptions _options;
    private readonly ILogger<SearchService> _logger;

    public SearchService(
        MeilisearchClient meilisearchClient,
        IOptions<MeilisearchOptions> options,
        ILogger<SearchService> logger)
    {
        _meilisearchClient = meilisearchClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<MeilisearchDocument>> Search(string query, int limit = 20, int offset = 0,
        string[]? attributesToRetrieve = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var index = _meilisearchClient.Index(_options.IndexName);
            var searchQuery = new SearchQuery
            {
                Limit = limit,
                Offset = offset,
                AttributesToRetrieve = attributesToRetrieve
            };
            
            var result = await index.SearchAsync<MeilisearchDocument>(query, searchQuery, cancellationToken);
            return result.Hits;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Meilisearch search failed for query '{Query}'", query);
            throw;
        }
    }
}

using System.ComponentModel;
using ModelContextProtocol.Server;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Mcp.Tools;

internal class SearchTools(ISearchService searchService)
{
    [McpServerTool]
    [Description("Searches indexed pages and returns matching documents.")]
    public async Task<IReadOnlyCollection<MeilisearchDocument>> SearchPages(
        [Description("The search query")]
        string query,
        [Description("Maximum number of results to return (default 20)")]
        int limit = 20,
        [Description("Number of results to skip for pagination (default 0)")]
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        string[] attributesToRetrieve = ["title", "canonical"];
        
        return await searchService.Search(query, limit, offset, attributesToRetrieve, cancellationToken);
    }
}

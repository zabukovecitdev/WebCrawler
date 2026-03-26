using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Services.Abstractions;

public interface ISearchService
{
    Task<IReadOnlyCollection<MeilisearchDocument>> Search(string query, int limit = 20, int offset = 0,
        string[]? attributesToRetrieve = null, CancellationToken cancellationToken = default);
}

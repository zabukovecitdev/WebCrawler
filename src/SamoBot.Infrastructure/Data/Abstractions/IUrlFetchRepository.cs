using System.Data;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Data.Abstractions;

public interface IUrlFetchRepository : IRepository<UrlFetch>
{
    Task<IEnumerable<UrlFetch>> GetUnparsedHtmlFetches(int limit, IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<bool> MarkAsParsed(int id, CancellationToken cancellationToken = default);
}

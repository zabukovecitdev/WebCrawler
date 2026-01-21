using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IUrlFetchService
{
    Task<FetchedContent> Fetch(string url, CancellationToken cancellationToken = default);
}

using Samobot.Domain.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IHtmlParser
{
    Task<ParsedDocument> Parse(MemoryStream htmlStream, CancellationToken cancellationToken = default);
}

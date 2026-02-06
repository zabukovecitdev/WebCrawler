using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Storage.Abstractions;

public interface IHtmlParser
{
    ParsedDocument Parse(MemoryStream htmlStream);
}

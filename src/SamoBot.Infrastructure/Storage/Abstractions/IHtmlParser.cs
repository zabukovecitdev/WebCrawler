namespace SamoBot.Infrastructure.Storage.Abstractions;

public class ParsedLink
{
    public string Url { get; set; } = string.Empty;
    public string LinkText { get; set; } = string.Empty;
}

public interface IHtmlParser
{
    Task<List<ParsedLink>> ParseAsync(MemoryStream htmlStream, CancellationToken cancellationToken = default);
}

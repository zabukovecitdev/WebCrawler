using System.Text;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public partial class HtmlParser : IHtmlParser
{
    private readonly ILogger<HtmlParser> _logger;

    public HtmlParser(ILogger<HtmlParser> logger)
    {
        _logger = logger;
    }

    public async Task<List<ParsedLink>> ParseAsync(MemoryStream htmlStream, CancellationToken cancellationToken = default)
    {
        var links = new List<ParsedLink>();

        try
        {
            htmlStream.Position = 0;

            var htmlDocument = new HtmlDocument();
            htmlDocument.Load(htmlStream, Encoding.UTF8);

            var anchorNodes = htmlDocument.DocumentNode.SelectNodes("//a[@href]");
            
            if (anchorNodes == null)
            {
                _logger.LogDebug("No anchor tags with href found in HTML");
                return links;
            }

            foreach (var anchorNode in anchorNodes)
            {
                var href = anchorNode.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrWhiteSpace(href))
                {
                    continue;
                }

                var linkText = anchorNode.InnerText?.Trim() ?? string.Empty;
                
                linkText = MyRegex().Replace(linkText, " ");

                links.Add(new ParsedLink
                {
                    Url = href,
                    LinkText = linkText
                });
            }

            _logger.LogDebug("Parsed {Count} links from HTML", links.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML stream");
            throw;
        }

        return links;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();
}

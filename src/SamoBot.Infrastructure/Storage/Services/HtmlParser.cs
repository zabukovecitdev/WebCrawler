using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Samobot.Domain.Models;
using SamoBot.Infrastructure.Storage.Abstractions;

namespace SamoBot.Infrastructure.Storage.Services;

public partial class HtmlParser : IHtmlParser
{
    private readonly ILogger<HtmlParser> _logger;
    private static readonly string[] UnwantedTags = { "script", "style", "noscript", "svg", "path" };

    public HtmlParser(ILogger<HtmlParser> logger)
    {
        _logger = logger;
    }

    public async Task<ParsedDocument> Parse(MemoryStream htmlStream, CancellationToken cancellationToken = default)
    {
        try
        {
            htmlStream.Position = 0;

            var htmlDocument = new HtmlDocument();
            htmlDocument.Load(htmlStream, Encoding.UTF8);

            var document = new ParsedDocument
            {
                Title = ExtractTitle(htmlDocument),
                Description = ExtractMetaDescription(htmlDocument),
                Keywords = ExtractMetaKeywords(htmlDocument),
                Author = ExtractMetaAuthor(htmlDocument),
                Language = ExtractLanguage(htmlDocument),
                Canonical = ExtractCanonical(htmlDocument),
                Headings = ExtractHeadings(htmlDocument),
                BodyText = ExtractBodyText(htmlDocument),
                Links = ExtractLinks(htmlDocument),
                Images = ExtractImages(htmlDocument),
                RobotsDirectives = ExtractRobotsDirectives(htmlDocument),
                OpenGraphData = ExtractOpenGraphData(htmlDocument),
                TwitterCardData = ExtractTwitterCardData(htmlDocument),
                JsonLdData = ExtractJsonLd(htmlDocument)
            };

            _logger.LogDebug(
                "Parsed HTML - Title: {Title}, Links: {LinkCount}, Images: {ImageCount}, Headings: {HeadingCount}",
                document.Title, 
                document.Links.Count, 
                document.Images.Count, 
                document.Headings.Count
            );

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML stream");
            throw;
        }
    }

    private string ExtractTitle(HtmlDocument htmlDoc)
    {
        var title = htmlDoc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? string.Empty;
        
        return System.Net.WebUtility.HtmlDecode(title);
    }

    private string ExtractMetaDescription(HtmlDocument htmlDoc)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        
        return node?.GetAttributeValue("content", string.Empty) ?? string.Empty;
    }

    private string ExtractMetaKeywords(HtmlDocument htmlDoc)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
        
        return node?.GetAttributeValue("content", string.Empty) ?? string.Empty;
    }

    private string ExtractMetaAuthor(HtmlDocument htmlDoc)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='author']");
        
        return node?.GetAttributeValue("content", string.Empty) ?? string.Empty;
    }

    private string ExtractLanguage(HtmlDocument htmlDoc)
    {
        var lang = htmlDoc.DocumentNode.SelectSingleNode("//html")?.GetAttributeValue("lang", string.Empty);
        if (!string.IsNullOrWhiteSpace(lang))
        {
            return lang;
        }

        var metaLang = htmlDoc.DocumentNode.SelectSingleNode("//meta[@http-equiv='content-language']");
        
        return metaLang?.GetAttributeValue("content", string.Empty) ?? string.Empty;
    }

    private string ExtractCanonical(HtmlDocument htmlDoc)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode("//link[@rel='canonical']");
        return node?.GetAttributeValue("href", string.Empty) ?? string.Empty;
    }

    private List<ParsedHeading> ExtractHeadings(HtmlDocument htmlDoc)
    {
        var headings = new List<ParsedHeading>();

        for (int level = 1; level <= 6; level++)
        {
            var nodes = htmlDoc.DocumentNode.SelectNodes($"//h{level}");
            if (nodes == null) continue;

            foreach (var node in nodes)
            {
                var text = System.Net.WebUtility.HtmlDecode(node.InnerText.Trim());
                if (!string.IsNullOrWhiteSpace(text))
                {
                    headings.Add(new ParsedHeading
                    {
                        Level = level,
                        Text = WhitespaceRegex().Replace(text, " ")
                    });
                }
            }
        }

        return headings;
    }

    private string ExtractBodyText(HtmlDocument htmlDoc)
    {
        // Clone to avoid modifying original
        var docCopy = new HtmlDocument();
        docCopy.LoadHtml(htmlDoc.DocumentNode.OuterHtml);

        // Remove unwanted elements
        docCopy.DocumentNode.Descendants()
            .Where(n => UnwantedTags.Contains(n.Name))
            .ToList()
            .ForEach(n => n.Remove());

        // Remove comments
        docCopy.DocumentNode.Descendants()
            .Where(n => n.NodeType == HtmlNodeType.Comment)
            .ToList()
            .ForEach(n => n.Remove());

        var body = docCopy.DocumentNode.SelectSingleNode("//body");
        if (body == null) return string.Empty;

        string text = body.InnerText;
        
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Normalize whitespace
        text = WhitespaceRegex().Replace(text, " ");
        
        // Collapse multiple newlines
        text = TextCleaner.CollapseEmptyLines(text);
        
        return text.Trim();
    }

    private List<ParsedLink> ExtractLinks(HtmlDocument htmlDoc)
    {
        var links = new List<ParsedLink>();
        var anchorNodes = htmlDoc.DocumentNode.SelectNodes("//a[@href]");

        if (anchorNodes == null) return links;

        foreach (var anchorNode in anchorNodes)
        {
            var href = anchorNode.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href))
                continue;

            var linkText = System.Net.WebUtility.HtmlDecode(anchorNode.InnerText?.Trim() ?? string.Empty);
            linkText = WhitespaceRegex().Replace(linkText, " ");

            var rel = anchorNode.GetAttributeValue("rel", string.Empty);
            var title = anchorNode.GetAttributeValue("title", string.Empty);

            links.Add(new ParsedLink
            {
                Url = href,
                LinkText = linkText,
                Rel = rel,
                Title = title,
                IsNoFollow = rel.Contains("nofollow", StringComparison.OrdinalIgnoreCase)
            });
        }

        return links;
    }

    private List<ParsedImage> ExtractImages(HtmlDocument htmlDoc)
    {
        var images = new List<ParsedImage>();
        var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img");

        if (imageNodes == null) return images;

        foreach (var imgNode in imageNodes)
        {
            var src = imgNode.GetAttributeValue("src", string.Empty);
            if (string.IsNullOrWhiteSpace(src))
                continue;

            images.Add(new ParsedImage
            {
                Src = src,
                Alt = imgNode.GetAttributeValue("alt", string.Empty),
                Title = imgNode.GetAttributeValue("title", string.Empty),
                Width = imgNode.GetAttributeValue("width", string.Empty),
                Height = imgNode.GetAttributeValue("height", string.Empty)
            });
        }

        return images;
    }

    private RobotsDirectives ExtractRobotsDirectives(HtmlDocument htmlDoc)
    {
        var node = htmlDoc.DocumentNode.SelectSingleNode("//meta[@name='robots']");
        var content = node?.GetAttributeValue("content", string.Empty) ?? string.Empty;

        return new RobotsDirectives
        {
            Content = content,
            NoIndex = content.Contains("noindex", StringComparison.OrdinalIgnoreCase),
            NoFollow = content.Contains("nofollow", StringComparison.OrdinalIgnoreCase),
            NoArchive = content.Contains("noarchive", StringComparison.OrdinalIgnoreCase),
            NoSnippet = content.Contains("nosnippet", StringComparison.OrdinalIgnoreCase)
        };
    }

    private Dictionary<string, string> ExtractOpenGraphData(HtmlDocument htmlDoc)
    {
        var ogData = new Dictionary<string, string>();
        var ogNodes = htmlDoc.DocumentNode.SelectNodes("//meta[starts-with(@property, 'og:')]");

        if (ogNodes == null) return ogData;

        foreach (var node in ogNodes)
        {
            var property = node.GetAttributeValue("property", string.Empty);
            var content = node.GetAttributeValue("content", string.Empty);
            
            if (!string.IsNullOrWhiteSpace(property) && !string.IsNullOrWhiteSpace(content))
            {
                ogData[property] = content;
            }
        }

        return ogData;
    }

    private Dictionary<string, string> ExtractTwitterCardData(HtmlDocument htmlDoc)
    {
        var twitterData = new Dictionary<string, string>();
        var twitterNodes = htmlDoc.DocumentNode.SelectNodes("//meta[starts-with(@name, 'twitter:')]");

        if (twitterNodes == null) return twitterData;

        foreach (var node in twitterNodes)
        {
            var name = node.GetAttributeValue("name", string.Empty);
            var content = node.GetAttributeValue("content", string.Empty);
            
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(content))
            {
                twitterData[name] = content;
            }
        }

        return twitterData;
    }

    private List<string> ExtractJsonLd(HtmlDocument htmlDoc)
    {
        var jsonLdScripts = new List<string>();
        var scriptNodes = htmlDoc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");

        if (scriptNodes == null) return jsonLdScripts;

        foreach (var script in scriptNodes)
        {
            var json = script.InnerText?.Trim();
            if (!string.IsNullOrWhiteSpace(json))
            {
                jsonLdScripts.Add(json);
            }
        }

        return jsonLdScripts;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
    
    public static partial class TextCleaner
    {
        public static string CollapseEmptyLines(string input)
        {
            return MyRegex1().Replace(input, "\n\n");
        }
        [GeneratedRegex(@"(\r?\n\s*){2,}")]
        private static partial Regex MyRegex1();
    }
}
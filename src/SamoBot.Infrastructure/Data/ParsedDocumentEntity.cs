using System.Text.Json;
using SamoBot.Infrastructure.Models;

namespace SamoBot.Infrastructure.Data;

/// <summary>
/// Persistence model for ParsedDocuments table. JSON columns are stored as strings;
/// use <see cref="ToParsedDocument"/> to convert to the domain model.
/// </summary>
public class ParsedDocumentEntity
{
    public int Id { get; set; }
    public int UrlFetchId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public string? Author { get; set; }
    public string? Language { get; set; }
    public string? Canonical { get; set; }
    public string? BodyText { get; set; }
    public string? Headings { get; set; }
    public string? Images { get; set; }
    public string? RobotsDirectives { get; set; }
    public string? OpenGraphData { get; set; }
    public string? TwitterCardData { get; set; }
    public string? JsonLdData { get; set; }
    public DateTimeOffset? ParsedAt { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public DateTimeOffset? IndexingStartedAt { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ParsedDocument ToParsedDocument()
    {
        return new ParsedDocument
        {
            Title = Title ?? string.Empty,
            Description = Description ?? string.Empty,
            Keywords = Keywords ?? string.Empty,
            Author = Author ?? string.Empty,
            Language = Language ?? string.Empty,
            Canonical = Canonical ?? string.Empty,
            BodyText = BodyText ?? string.Empty,
            Headings = DeserializeJson<List<ParsedHeading>>(Headings),
            Links = [], // Not stored in DB
            Images = DeserializeJson<List<ParsedImage>>(Images),
            RobotsDirectives = DeserializeJson<RobotsDirectives>(RobotsDirectives) ?? new RobotsDirectives(),
            OpenGraphData = DeserializeJson<Dictionary<string, string>>(OpenGraphData) ?? new Dictionary<string, string>(),
            TwitterCardData = DeserializeJson<Dictionary<string, string>>(TwitterCardData) ?? new Dictionary<string, string>(),
            JsonLdData = DeserializeJson<List<string>>(JsonLdData) ?? new List<string>()
        };
    }

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }
}

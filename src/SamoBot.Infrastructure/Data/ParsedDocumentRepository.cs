using System.Text.Json;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Models;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class ParsedDocumentRepository(QueryFactory queryFactory) : IParsedDocumentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<int> SaveParsedDocument(int urlFetchId, ParsedDocument parsedDocument, CancellationToken cancellationToken = default)
    {
        var id = await queryFactory.Query(TableNames.Database.ParsedDocuments)
            .InsertGetIdAsync<int>(new
            {
                UrlFetchId = urlFetchId,
                parsedDocument.Title,
                parsedDocument.Description,
                parsedDocument.Keywords,
                parsedDocument.Author,
                parsedDocument.Language,
                parsedDocument.Canonical,
                parsedDocument.BodyText,
                Headings = JsonSerializer.Serialize(parsedDocument.Headings, JsonOptions),
                Images = JsonSerializer.Serialize(parsedDocument.Images, JsonOptions),
                RobotsDirectives = JsonSerializer.Serialize(parsedDocument.RobotsDirectives, JsonOptions),
                OpenGraphData = JsonSerializer.Serialize(parsedDocument.OpenGraphData, JsonOptions),
                TwitterCardData = JsonSerializer.Serialize(parsedDocument.TwitterCardData, JsonOptions),
                JsonLdData = JsonSerializer.Serialize(parsedDocument.JsonLdData, JsonOptions),
                ParsedAt = DateTimeOffset.UtcNow
            }, cancellationToken: cancellationToken);

        return id;
    }

    public async Task<ParsedDocument?> GetByUrlFetchId(int urlFetchId, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableNames.Database.ParsedDocuments)
            .Where("UrlFetchId", urlFetchId)
            .FirstOrDefaultAsync<dynamic>(cancellationToken: cancellationToken);

        if (result == null)
        {
            return null;
        }

        return new ParsedDocument
        {
            Title = result.Title ?? string.Empty,
            Description = result.Description ?? string.Empty,
            Keywords = result.Keywords ?? string.Empty,
            Author = result.Author ?? string.Empty,
            Language = result.Language ?? string.Empty,
            Canonical = result.Canonical ?? string.Empty,
            BodyText = result.BodyText ?? string.Empty,
            Headings = DeserializeJson<List<ParsedHeading>>(result.Headings),
            Links = new List<ParsedLink>(), // Links are not stored, they're sent to queue
            Images = DeserializeJson<List<ParsedImage>>(result.Images),
            RobotsDirectives = DeserializeJson<RobotsDirectives>(result.RobotsDirectives) ?? new RobotsDirectives(),
            OpenGraphData = DeserializeJson<Dictionary<string, string>>(result.OpenGraphData) ?? new Dictionary<string, string>(),
            TwitterCardData = DeserializeJson<Dictionary<string, string>>(result.TwitterCardData) ?? new Dictionary<string, string>(),
            JsonLdData = DeserializeJson<List<string>>(result.JsonLdData) ?? new List<string>()
        };
    }

    private static T? DeserializeJson<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

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

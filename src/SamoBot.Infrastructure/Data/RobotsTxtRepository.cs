using System.Text.Json;
using RobotsTxtModel = SamoBot.Infrastructure.Models.RobotsTxt;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Models;
using SqlKata.Execution;

namespace SamoBot.Infrastructure.Data;

public class RobotsTxtRepository(QueryFactory queryFactory) : IRobotsTxtRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<RobotsTxtModel?> GetByHostAsync(string host, CancellationToken cancellationToken = default)
    {
        var result = await queryFactory.Query(TableNames.Database.RobotsTxt)
            .Where("Host", host)
            .Where("ExpiresAt", ">", DateTimeOffset.UtcNow)
            .FirstOrDefaultAsync<dynamic>(cancellationToken: cancellationToken);

        if (result == null)
        {
            return null;
        }

        return MapFromDb(result);
    }

    public async Task SaveAsync(RobotsTxtModel robotsTxt, CancellationToken cancellationToken = default)
    {
        // Use upsert pattern - try to update first, insert if not found
        var existing = await queryFactory.Query(TableNames.Database.RobotsTxt)
            .Where("Host", robotsTxt.Host)
            .FirstOrDefaultAsync<dynamic>(cancellationToken: cancellationToken);

        var parsedRulesJson = JsonSerializer.Serialize(robotsTxt.Rules, JsonOptions);
        using var parsedRulesDoc = JsonDocument.Parse(parsedRulesJson);
        var data = new
        {
            Host = robotsTxt.Host,
            Content = robotsTxt.Content,
            ParsedRules = parsedRulesDoc.RootElement,
            FetchedAt = robotsTxt.FetchedAt,
            ExpiresAt = robotsTxt.ExpiresAt,
            CrawlDelayMs = robotsTxt.CrawlDelayMs,
            IsFetchError = robotsTxt.IsFetchError,
            ErrorMessage = robotsTxt.ErrorMessage,
            StatusCode = robotsTxt.StatusCode
        };

        if (existing != null)
        {
            await queryFactory.Query(TableNames.Database.RobotsTxt)
                .Where("Host", robotsTxt.Host)
                .UpdateAsync(data, cancellationToken: cancellationToken);

            robotsTxt.Id = (int)existing.Id;
        }
        else
        {
            var id = await queryFactory.Query(TableNames.Database.RobotsTxt)
                .InsertGetIdAsync<int>(data, cancellationToken: cancellationToken);

            robotsTxt.Id = id;
        }
    }

    public async Task<List<RobotsTxtModel>> GetExpiringAsync(TimeSpan expiresWithin, int limit, CancellationToken cancellationToken = default)
    {
        var expiresBy = DateTimeOffset.UtcNow.Add(expiresWithin);

        var results = await queryFactory.Query(TableNames.Database.RobotsTxt)
            .Where("ExpiresAt", "<=", expiresBy)
            .Where("ExpiresAt", ">", DateTimeOffset.UtcNow) // Not yet expired
            .OrderBy("ExpiresAt")
            .Limit(limit)
            .GetAsync<dynamic>(cancellationToken: cancellationToken);

        return results.Select(MapFromDb).ToList();
    }

    private static RobotsTxtModel MapFromDb(dynamic row)
    {
        return new RobotsTxtModel
        {
            Id = (int)row.Id,
            Host = row.Host ?? string.Empty,
            Content = row.Content ?? string.Empty,
            Rules = DeserializeJson<List<RobotsTxtRule>>(row.ParsedRules) ?? new List<RobotsTxtRule>(),
            FetchedAt = ((DateTimeOffset)row.FetchedAt).DateTime,
            ExpiresAt = ((DateTimeOffset)row.ExpiresAt).DateTime,
            CrawlDelayMs = row.CrawlDelayMs,
            SitemapUrls = new List<string>(), // Not stored in DB yet
            IsFetchError = row.IsFetchError,
            ErrorMessage = row.ErrorMessage,
            StatusCode = row.StatusCode
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

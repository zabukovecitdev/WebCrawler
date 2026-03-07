using System.Data;
using System.Text.Json;
using Dapper;
using SamoBot.Infrastructure.Constants;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Extensions;
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
        var entity = await queryFactory.Query(TableNames.Database.ParsedDocuments)
            .Where("UrlFetchId", urlFetchId)
            .FirstOrDefaultAsync<ParsedDocumentEntity>(cancellationToken: cancellationToken);

        return entity?.ToParsedDocument();
    }

    public async Task<IEnumerable<ParsedDocumentEntity>> GetUnindexed(int limit,
        CancellationToken cancellationToken = default)
    {
        var entities = await queryFactory.Query(TableNames.Database.ParsedDocuments)
            .WhereNull(nameof(ParsedDocumentEntity.IndexedAt))
            .OrderBy(nameof(ParsedDocumentEntity.Id))
            .Limit(limit)
            .GetAsync<ParsedDocumentEntity>(cancellationToken: cancellationToken);

        return entities;
    }

    public async Task<IEnumerable<ParsedDocumentEntity>> GetAndClaimUnindexed(int limit, DateTimeOffset staleClaimBefore, IDbTransaction transaction, CancellationToken cancellationToken = default)
    {
        if (transaction?.Connection == null)
            throw new InvalidOperationException("Transaction and connection must be provided for FOR UPDATE SKIP LOCKED");

        var query = queryFactory.Query(TableNames.Database.ParsedDocuments)
            .WhereNull(nameof(ParsedDocumentEntity.IndexedAt))
            .Where(q => q
                .WhereNull(nameof(ParsedDocumentEntity.IndexingStartedAt))
                .OrWhere(nameof(ParsedDocumentEntity.IndexingStartedAt), "<", staleClaimBefore))
            .OrderBy(nameof(ParsedDocumentEntity.Id))
            .Limit(limit)
            .ForUpdateSkipLocked();

        var sqlResult = queryFactory.Compiler.Compile(query);
        var command = new CommandDefinition(sqlResult.Sql, sqlResult.NamedBindings, transaction, cancellationToken: cancellationToken);
        var entities = (await transaction.Connection!.QueryAsync<ParsedDocumentEntity>(command)).ToList();

        if (entities.Count == 0)
            return entities;

        var now = DateTimeOffset.UtcNow;
        var ids = entities.Select(e => e.Id).ToList();
        const string updateSql = """
            UPDATE "ParsedDocuments"
            SET "IndexingStartedAt" = @Now
            WHERE "Id" = ANY(@Ids)
            """;
        await transaction.Connection.ExecuteAsync(
            new CommandDefinition(updateSql, new { Now = now, Ids = ids.ToArray() }, transaction, cancellationToken: cancellationToken));

        return entities;
    }

    public async Task MarkAsIndexed(IEnumerable<int> parsedDocumentIds, CancellationToken cancellationToken = default)
    {
        var ids = parsedDocumentIds.ToList();
        if (ids.Count == 0)
            return;

        await queryFactory.Query(TableNames.Database.ParsedDocuments)
            .WhereIn(nameof(ParsedDocumentEntity.Id), ids)
            .UpdateAsync(new { IndexedAt = DateTimeOffset.UtcNow, IndexingStartedAt = (DateTimeOffset?)null }, cancellationToken: cancellationToken);
    }

    public async Task<int> GetAll(int urlFetchId, ParsedDocument parsedDocument, CancellationToken cancellationToken = default)
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
    
}

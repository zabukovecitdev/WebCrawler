using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Meilisearch;
using SamoBot.Infrastructure.Data;
using SamoBot.Infrastructure.Data.Abstractions;
using SamoBot.Infrastructure.Models;
using SamoBot.Infrastructure.Options;
using SamoBot.Infrastructure.Services.Abstractions;

namespace SamoBot.Infrastructure.Services;

public class IndexService : IIndexerService
{
    private readonly IParsedDocumentRepository _parsedDocumentRepository;
    private readonly MeilisearchClient _meilisearchClient;
    private readonly MeilisearchOptions _options;
    private readonly ILogger<IndexService> _logger;

    public IndexService(
        IParsedDocumentRepository parsedDocumentRepository,
        MeilisearchClient meilisearchClient,
        IOptions<MeilisearchOptions> options,
        ILogger<IndexService> logger)
    {
        _parsedDocumentRepository = parsedDocumentRepository;
        _meilisearchClient = meilisearchClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task Index(IEnumerable<ParsedDocumentEntity> documents, CancellationToken cancellationToken = default)
    {
        var list = documents.ToList();
        if (list.Count == 0)
            return;

        var meilisearchDocs = list.Select(e => new MeilisearchDocument
        {
            Id = e.Id.ToString(),
            UrlFetchId = e.UrlFetchId,
            Title = e.Title ?? string.Empty,
            Description = e.Description ?? string.Empty,
            Keywords = e.Keywords ?? string.Empty,
            Author = e.Author ?? string.Empty,
            Language = e.Language ?? string.Empty,
            Canonical = e.Canonical ?? string.Empty,
            BodyText = e.BodyText ?? string.Empty
        }).ToList();

        try
        {
            var index = _meilisearchClient.Index(_options.IndexName);
            await index.AddDocumentsAsync(meilisearchDocs, cancellationToken: cancellationToken);
            var ids = list.Select(d => d.Id).ToList();
            await _parsedDocumentRepository.MarkAsIndexed(ids, cancellationToken);
            _logger.LogInformation("Indexed {Count} documents in Meilisearch", list.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Meilisearch unreachable or error; skipping index of {Count} documents", list.Count);
        }
    }
}
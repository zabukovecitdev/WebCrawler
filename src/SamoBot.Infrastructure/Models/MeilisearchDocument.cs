namespace SamoBot.Infrastructure.Models;

/// <summary>
/// Document shape sent to Meilisearch for the parsed_documents index.
/// </summary>
public class MeilisearchDocument
{
    public string Id { get; set; } = string.Empty;
    public int UrlFetchId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Canonical { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
}

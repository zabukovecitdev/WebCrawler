namespace SamoBot.Infrastructure.Options;

public class MeilisearchOptions
{
    public const string SectionName = "Meilisearch";

    public string Host { get; set; } = "http://localhost:7700";
    public string ApiKey { get; set; } = "masterKey";
    public string IndexName { get; set; } = "parsed_documents";
}

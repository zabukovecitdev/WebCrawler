using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000008, "Create index for unparsed HTML fetches query")]
public class CreateIndexForUnparsedHtmlFetches : Migration
{
    public override void Up()
    {
        // Create a partial composite index optimized for GetUnparsedHtmlFetches query
        // The index covers:
        // - ContentType (for filtering text/html)
        // - FetchedAt (for ordering)
        // Partial index condition: ParsedAt IS NULL AND ObjectName IS NOT NULL
        Execute.Sql($@"
            CREATE INDEX IX_UrlFetches_UnparsedHtml 
            ON ""{TableNames.Database.UrlFetches}"" (""ContentType"", ""FetchedAt"")
            WHERE ""ParsedAt"" IS NULL AND ""ObjectName"" IS NOT NULL;
        ");
    }

    public override void Down()
    {
        Delete.Index("IX_UrlFetches_UnparsedHtml")
            .OnTable(TableNames.Database.UrlFetches);
    }
}

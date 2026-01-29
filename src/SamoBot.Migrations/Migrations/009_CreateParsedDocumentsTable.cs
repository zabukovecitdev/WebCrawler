using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000009, "Create ParsedDocuments table")]
public class CreateParsedDocumentsTable : Migration
{
    public override void Up()
    {
        // Drop index if it exists from a previous partial migration run
        // (This index shouldn't exist since .Unique() on column creates a constraint, not this named index)
        Execute.Sql($@"DROP INDEX IF EXISTS ""public"".""IX_ParsedDocuments_UrlFetchId"";");

        // Check if table already exists (from partial migration run)
        if (!Schema.Table(TableNames.Database.ParsedDocuments).Exists())
        {
            Create.Table(TableNames.Database.ParsedDocuments)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UrlFetchId").AsInt32().NotNullable().Unique()
            .WithColumn("Title").AsString(500).Nullable()
            .WithColumn("Description").AsString(2000).Nullable()
            .WithColumn("Keywords").AsString(1000).Nullable()
            .WithColumn("Author").AsString(255).Nullable()
            .WithColumn("Language").AsString(10).Nullable()
            .WithColumn("Canonical").AsString(2048).Nullable()
            .WithColumn("BodyText").AsString().Nullable() // TEXT type for large content
            .WithColumn("Headings").AsString().Nullable() // JSON array of ParsedHeading
            .WithColumn("Images").AsString().Nullable() // JSON array of ParsedImage
            .WithColumn("RobotsDirectives").AsString().Nullable() // JSON object
            .WithColumn("OpenGraphData").AsString().Nullable() // JSON object
            .WithColumn("TwitterCardData").AsString().Nullable() // JSON object
            .WithColumn("JsonLdData").AsString().Nullable() // JSON array of strings
                .WithColumn("ParsedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.ForeignKey("FK_ParsedDocuments_UrlFetches")
                .FromTable(TableNames.Database.ParsedDocuments)
                .ForeignColumn("UrlFetchId")
                .ToTable(TableNames.Database.UrlFetches)
                .PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);
        }

        // Create index if it doesn't exist (idempotent)
        if (!Schema.Table(TableNames.Database.ParsedDocuments).Index("IX_ParsedDocuments_ParsedAt").Exists())
        {
            Create.Index("IX_ParsedDocuments_ParsedAt")
                .OnTable(TableNames.Database.ParsedDocuments)
                .OnColumn("ParsedAt");
        }
    }

    public override void Down()
    {
        Delete.Table(TableNames.Database.ParsedDocuments);
    }
}

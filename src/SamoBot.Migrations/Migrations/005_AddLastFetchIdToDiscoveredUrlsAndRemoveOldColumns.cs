using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000005, "Add LastFetchId to DiscoveredUrls and remove old fetch metadata columns")]
public class AddLastFetchIdToDiscoveredUrlsAndRemoveOldColumns : Migration
{
    public override void Up()
    {
        // Add LastFetchId column
        Alter.Table(TableNames.Database.DiscoveredUrls)
            .AddColumn("LastFetchId").AsInt32().Nullable();

        // Create foreign key to UrlFetches
        Create.ForeignKey("FK_DiscoveredUrls_UrlFetches")
            .FromTable(TableNames.Database.DiscoveredUrls)
            .ForeignColumn("LastFetchId")
            .ToTable(TableNames.Database.UrlFetches)
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        // Create index for LastFetchId
        Create.Index("IX_DiscoveredUrls_LastFetchId")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .OnColumn("LastFetchId");

        // Remove old columns (migrate data first if needed)
        // Note: In production, you might want to migrate existing data to UrlFetches table first
        Delete.Column("ContentType")
            .Column("ContentLength")
            .Column("ObjectName")
            .Column("LastStatusCode")
            .FromTable(TableNames.Database.DiscoveredUrls);
    }

    public override void Down()
    {
        // Restore old columns
        Alter.Table(TableNames.Database.DiscoveredUrls)
            .AddColumn("LastStatusCode").AsInt16().Nullable()
            .AddColumn("ContentType").AsString(255).Nullable()
            .AddColumn("ContentLength").AsInt64().Nullable()
            .AddColumn("ObjectName").AsString(2048).Nullable();

        // Remove new column and foreign key
        Delete.ForeignKey("FK_DiscoveredUrls_UrlFetches")
            .OnTable(TableNames.Database.DiscoveredUrls);
        
        Delete.Index("IX_DiscoveredUrls_LastFetchId")
            .OnTable(TableNames.Database.DiscoveredUrls);
        
        Delete.Column("LastFetchId")
            .FromTable(TableNames.Database.DiscoveredUrls);
    }
}

using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000001, "Create DiscoveredUrls table")]
public class CreateDiscoveredUrlsTable : Migration
{
    public override void Up()
    {
        Create.Table(TableNames.Database.DiscoveredUrls)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Host").AsString(2048).NotNullable()
            .WithColumn("Url").AsString(2048).NotNullable()
            .WithColumn("NormalizedUrl").AsString(2048).Nullable()
            .WithColumn("Status").AsString(20).NotNullable().WithDefaultValue("Idle")
            .WithColumn("LastCrawlAt").AsDateTimeOffset().Nullable()
            .WithColumn("NextCrawlAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("FailCount").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("LastStatus").AsInt16().Nullable()
            .WithColumn("DiscoveredAt").AsDateTime().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("Priority").AsInt16().NotNullable().WithDefaultValue(0);

        Create.Index("IX_DiscoveredUrls_NormalizedUrl")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .OnColumn("NormalizedUrl");

        Create.Index("IX_DiscoveredUrls_Url")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .OnColumn("Url");

        Create.UniqueConstraint("IX_DiscoveredUrls_NormalizedUrl_Unique")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .Columns("NormalizedUrl");
    }

    public override void Down()
    {
        Delete.Table(TableNames.Database.DiscoveredUrls);
    }
}

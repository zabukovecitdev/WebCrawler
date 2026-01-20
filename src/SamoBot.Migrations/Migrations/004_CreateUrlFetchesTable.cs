using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000004, "Create UrlFetches table")]
public class CreateUrlFetchesTable : Migration
{
    public override void Up()
    {
        Create.Table(TableNames.Database.UrlFetches)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DiscoveredUrlId").AsInt32().NotNullable()
            .WithColumn("FetchedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("StatusCode").AsInt16().NotNullable()
            .WithColumn("ContentType").AsString(255).Nullable()
            .WithColumn("ContentLength").AsInt64().Nullable()
            .WithColumn("ObjectName").AsString(2048).Nullable();

        Create.ForeignKey("FK_UrlFetches_DiscoveredUrls")
            .FromTable(TableNames.Database.UrlFetches)
            .ForeignColumn("DiscoveredUrlId")
            .ToTable(TableNames.Database.DiscoveredUrls)
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_UrlFetches_DiscoveredUrlId")
            .OnTable(TableNames.Database.UrlFetches)
            .OnColumn("DiscoveredUrlId");

        Create.Index("IX_UrlFetches_FetchedAt")
            .OnTable(TableNames.Database.UrlFetches)
            .OnColumn("FetchedAt");
    }

    public override void Down()
    {
        Delete.Table(TableNames.Database.UrlFetches);
    }
}

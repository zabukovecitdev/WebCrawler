using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000002, "Add content metadata columns to DiscoveredUrls table")]
public class AddContentMetadataToDiscoveredUrls : Migration
{
    public override void Up()
    {
        Alter.Table(TableNames.Database.DiscoveredUrls)
            .AddColumn("ContentType").AsString(255).Nullable()
            .AddColumn("ContentLength").AsInt64().Nullable()
            .AddColumn("ObjectName").AsString(2048).Nullable();
    }

    public override void Down()
    {
        Delete.Column("ContentType")
            .Column("ContentLength")
            .Column("ObjectName")
            .FromTable(TableNames.Database.DiscoveredUrls);
    }
}

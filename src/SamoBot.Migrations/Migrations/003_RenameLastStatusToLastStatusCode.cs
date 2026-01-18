using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000003, "Rename LastStatus column to LastStatusCode")]
public class RenameLastStatusToLastStatusCode : Migration
{
    public override void Up()
    {
        Rename.Column("LastStatus")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .To("LastStatusCode");
    }

    public override void Down()
    {
        Rename.Column("LastStatusCode")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .To("LastStatus");
    }
}

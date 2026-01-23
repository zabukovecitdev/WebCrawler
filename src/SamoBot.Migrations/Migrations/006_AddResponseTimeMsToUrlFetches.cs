using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000006, "Add ResponseTimeMs to UrlFetches table")]
public class AddResponseTimeMsToUrlFetches : Migration
{
    public override void Up()
    {
        Alter.Table(TableNames.Database.UrlFetches)
            .AddColumn("ResponseTimeMs").AsInt64().Nullable();
    }

    public override void Down()
    {
        Delete.Column("ResponseTimeMs").FromTable(TableNames.Database.UrlFetches);
    }
}

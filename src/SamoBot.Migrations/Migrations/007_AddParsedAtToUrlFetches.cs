using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000007, "Add ParsedAt to UrlFetches table")]
public class AddParsedAtToUrlFetches : Migration
{
    public override void Up()
    {
        Alter.Table(TableNames.Database.UrlFetches)
            .AddColumn("ParsedAt").AsDateTimeOffset().Nullable();
        
        Create.Index("IX_UrlFetches_ParsedAt")
            .OnTable(TableNames.Database.UrlFetches)
            .OnColumn("ParsedAt");
    }

    public override void Down()
    {
        Delete.Index("IX_UrlFetches_ParsedAt")
            .OnTable(TableNames.Database.UrlFetches);
        
        Delete.Column("ParsedAt").FromTable(TableNames.Database.UrlFetches);
    }
}

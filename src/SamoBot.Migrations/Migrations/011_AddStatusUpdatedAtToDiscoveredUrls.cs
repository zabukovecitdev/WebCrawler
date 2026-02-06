using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000011, "Add StatusUpdatedAt to DiscoveredUrls for orphaned InFlight detection")]
public class AddStatusUpdatedAtToDiscoveredUrls : Migration
{
    public override void Up()
    {
        Alter.Table(TableNames.Database.DiscoveredUrls)
            .AddColumn("StatusUpdatedAt").AsDateTimeOffset().Nullable();

        Execute.Sql($@"
            UPDATE ""{TableNames.Database.DiscoveredUrls}""
            SET ""StatusUpdatedAt"" = NOW() AT TIME ZONE 'UTC'
            WHERE ""Status"" = 'InFlight' AND ""StatusUpdatedAt"" IS NULL");
    }

    public override void Down()
    {
        Delete.Column("StatusUpdatedAt")
            .FromTable(TableNames.Database.DiscoveredUrls);
    }
}

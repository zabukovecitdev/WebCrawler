using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000010, "Create RobotsTxt table")]
public class CreateRobotsTxtTable : Migration
{
    public override void Up()
    {
        if (!Schema.Table(TableNames.Database.RobotsTxt).Exists())
        {
            Create.Table(TableNames.Database.RobotsTxt)
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("Host").AsString(2048).NotNullable()
                .WithColumn("Content").AsString().NotNullable() // TEXT type for full robots.txt content
                .WithColumn("ParsedRules").AsCustom("JSONB").NotNullable() // Parsed rules as JSON
                .WithColumn("FetchedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("ExpiresAt").AsDateTimeOffset().NotNullable()
                .WithColumn("CrawlDelayMs").AsInt32().Nullable()
                .WithColumn("IsFetchError").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("ErrorMessage").AsString().Nullable() // TEXT type for error details
                .WithColumn("StatusCode").AsInt32().Nullable();

            Create.UniqueConstraint("UQ_RobotsTxt_Host")
                .OnTable(TableNames.Database.RobotsTxt)
                .Column("Host");

            Create.Index("IX_RobotsTxt_ExpiresAt")
                .OnTable(TableNames.Database.RobotsTxt)
                .OnColumn("ExpiresAt");
        }
    }

    public override void Down()
    {
        Delete.Table(TableNames.Database.RobotsTxt);
    }
}

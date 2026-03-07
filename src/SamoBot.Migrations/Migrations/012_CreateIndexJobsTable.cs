using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000012, "Create IndexJobs table")]
public class CreateIndexJobsTable : Migration
{
    public override void Up()
    {
        Create.Table(TableNames.Database.IndexJobs)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DocumentId").AsInt32().NotNullable()
            .WithColumn("Operation").AsString(50).NotNullable()
            .WithColumn("Status").AsString(50).NotNullable().WithDefaultValue("Pending")
            .WithColumn("Attempts").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastError").AsString().Nullable()
            .WithColumn("ScheduledAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("ProcessedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        Create.ForeignKey("FK_IndexJobs_ParsedDocuments")
            .FromTable(TableNames.Database.IndexJobs)
            .ForeignColumn("DocumentId")
            .ToTable(TableNames.Database.ParsedDocuments)
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_IndexJobs_Status_ScheduledAt")
            .OnTable(TableNames.Database.IndexJobs)
            .OnColumn("Status").Ascending()
            .OnColumn("ScheduledAt").Ascending();

        Create.Index("IX_IndexJobs_DocumentId")
            .OnTable(TableNames.Database.IndexJobs)
            .OnColumn("DocumentId");
    }

    public override void Down()
    {
        Delete.Table(TableNames.Database.IndexJobs);
    }
}

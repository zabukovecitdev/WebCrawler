using FluentMigrator;
using SamoBot.Migrations.Constants;

namespace SamoBot.Migrations.Migrations;

[Migration(20240101000013, "Create CrawlJobs, CrawlJobEvents, extend DiscoveredUrls for jobs and depth")]
public class CreateCrawlJobsAndEvents : Migration
{
    public override void Up()
    {
        Create.Table(TableNames.Database.CrawlJobs)
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OwnerUserId").AsString(256).Nullable()
            .WithColumn("Status").AsString(50).NotNullable().WithDefaultValue("Draft")
            .WithColumn("SeedUrls").AsString().NotNullable().WithDefaultValue("[]")
            .WithColumn("MaxDepth").AsInt32().Nullable()
            .WithColumn("MaxUrls").AsInt32().Nullable()
            .WithColumn("UseJsRendering").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("RespectRobots").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("UpdatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
            .WithColumn("StartedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CompletedAt").AsDateTimeOffset().Nullable();

        Create.Index("IX_CrawlJobs_Status")
            .OnTable(TableNames.Database.CrawlJobs)
            .OnColumn("Status").Ascending();

        Create.Table(TableNames.Database.CrawlJobEvents)
            .WithColumn("Id").AsInt64().PrimaryKey().Identity()
            .WithColumn("CrawlJobId").AsInt32().NotNullable()
            .WithColumn("EventType").AsString(100).NotNullable()
            .WithColumn("Payload").AsString().NotNullable().WithDefaultValue("{}")
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

        Create.ForeignKey("FK_CrawlJobEvents_CrawlJobs")
            .FromTable(TableNames.Database.CrawlJobEvents)
            .ForeignColumn("CrawlJobId")
            .ToTable(TableNames.Database.CrawlJobs)
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_CrawlJobEvents_CrawlJobId_Id")
            .OnTable(TableNames.Database.CrawlJobEvents)
            .OnColumn("CrawlJobId").Ascending()
            .OnColumn("Id").Ascending();

        Alter.Table(TableNames.Database.DiscoveredUrls)
            .AddColumn("CrawlJobId").AsInt32().Nullable()
            .AddColumn("Depth").AsInt32().NotNullable().WithDefaultValue(0)
            .AddColumn("UseJsRendering").AsBoolean().NotNullable().WithDefaultValue(false)
            .AddColumn("RespectRobots").AsBoolean().NotNullable().WithDefaultValue(true);

        Create.ForeignKey("FK_DiscoveredUrls_CrawlJobs")
            .FromTable(TableNames.Database.DiscoveredUrls)
            .ForeignColumn("CrawlJobId")
            .ToTable(TableNames.Database.CrawlJobs)
            .PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.Index("IX_DiscoveredUrls_CrawlJobId")
            .OnTable(TableNames.Database.DiscoveredUrls)
            .OnColumn("CrawlJobId").Ascending();
    }

    public override void Down()
    {
        Delete.Index("IX_DiscoveredUrls_CrawlJobId")
            .OnTable(TableNames.Database.DiscoveredUrls);

        Delete.ForeignKey("FK_DiscoveredUrls_CrawlJobs")
            .OnTable(TableNames.Database.DiscoveredUrls);

        Delete.Column("CrawlJobId").FromTable(TableNames.Database.DiscoveredUrls);
        Delete.Column("Depth").FromTable(TableNames.Database.DiscoveredUrls);
        Delete.Column("UseJsRendering").FromTable(TableNames.Database.DiscoveredUrls);
        Delete.Column("RespectRobots").FromTable(TableNames.Database.DiscoveredUrls);

        Delete.Table(TableNames.Database.CrawlJobEvents);
        Delete.Table(TableNames.Database.CrawlJobs);
    }
}

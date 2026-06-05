using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PoliceBackend.Database;

#nullable disable

namespace PoliceBackend.Migrations;

[DbContext(typeof(IncidentDbContext))]
[Migration("20260605023000_DropIncidentSeverityFields")]
public partial class DropIncidentSeverityFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.Sql("""
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_Level' AND object_id = OBJECT_ID(N'[Incidents]'))
    DROP INDEX [IX_Incidents_Level] ON [Incidents];

IF COL_LENGTH(N'[Incidents]', N'Level') IS NOT NULL
    ALTER TABLE [Incidents] DROP COLUMN [Level];

IF COL_LENGTH(N'[Incidents]', N'UrgencyScore') IS NOT NULL
    ALTER TABLE [Incidents] DROP COLUMN [UrgencyScore];

IF COL_LENGTH(N'[Incidents]', N'ClassificationReason') IS NOT NULL
    ALTER TABLE [Incidents] DROP COLUMN [ClassificationReason];

IF COL_LENGTH(N'[Incidents]', N'InternalNote') IS NOT NULL
    ALTER TABLE [Incidents] DROP COLUMN [InternalNote];
""");
            return;
        }

        if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            migrationBuilder.Sql("""
DROP INDEX IF EXISTS "IX_Incidents_Level";

ALTER TABLE "Incidents" DROP COLUMN IF EXISTS "Level";
ALTER TABLE "Incidents" DROP COLUMN IF EXISTS "UrgencyScore";
ALTER TABLE "Incidents" DROP COLUMN IF EXISTS "ClassificationReason";
ALTER TABLE "Incidents" DROP COLUMN IF EXISTS "InternalNote";
""");
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer")
        {
            return;
        }

        migrationBuilder.AddColumn<string>(
            name: "Level",
            table: "Incidents",
            type: "nvarchar(24)",
            maxLength: 24,
            nullable: false,
            defaultValue: "high");

        migrationBuilder.AddColumn<int>(
            name: "UrgencyScore",
            table: "Incidents",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ClassificationReason",
            table: "Incidents",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "InternalNote",
            table: "Incidents",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_Incidents_Level",
            table: "Incidents",
            column: "Level");
    }
}

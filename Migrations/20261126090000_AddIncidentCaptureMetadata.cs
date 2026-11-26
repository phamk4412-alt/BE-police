using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PoliceBackend.Database;

namespace PoliceBackend.Migrations;

[DbContext(typeof(IncidentDbContext))]
[Migration("20261126090000_AddIncidentCaptureMetadata")]
public sealed class AddIncidentCaptureMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The existing bootstrapper creates Incidents after migrations on a fresh database.
        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            migrationBuilder.Sql("""
IF OBJECT_ID(N'[Incidents]', N'U') IS NOT NULL AND COL_LENGTH(N'[Incidents]', N'CaptureMetadata') IS NULL
    ALTER TABLE [Incidents] ADD [CaptureMetadata] nvarchar(max) NULL;
""");
        else if (ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
            migrationBuilder.Sql("""ALTER TABLE IF EXISTS "Incidents" ADD COLUMN IF NOT EXISTS "CaptureMetadata" text NULL;""");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("CaptureMetadata", "Incidents");
}

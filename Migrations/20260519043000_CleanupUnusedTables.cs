using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PoliceBackend.Database;

#nullable disable

namespace PoliceBackend.Migrations;

[DbContext(typeof(IncidentDbContext))]
[Migration("20260519043000_CleanupUnusedTables")]
public partial class CleanupUnusedTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[Accounts];
END;
""");

        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[NationalEvents]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[NationalEvents];
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}

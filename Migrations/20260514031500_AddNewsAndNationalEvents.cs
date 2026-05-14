using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PoliceBackend.Database;

#nullable disable

namespace PoliceBackend.Migrations;

[DbContext(typeof(IncidentDbContext))]
[Migration("20260514031500_AddNewsAndNationalEvents")]
public partial class AddNewsAndNationalEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NationalEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                SortOrder = table.Column<int>(type: "int", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NationalEvents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "News",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                FeaturedOrder = table.Column<int>(type: "int", nullable: true),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_News", x => x.Id);
                table.CheckConstraint("CK_News_FeaturedOrder", "([IsFeatured] = CAST(0 AS bit) AND [FeaturedOrder] IS NULL) OR ([IsFeatured] = CAST(1 AS bit) AND [FeaturedOrder] BETWEEN 1 AND 4)");
                table.CheckConstraint("CK_News_Status", "[Status] IN (N'draft', N'published', N'hidden')");
            });

        migrationBuilder.InsertData(
            table: "NationalEvents",
            columns: new[] { "Id", "Name", "EventDate", "Description", "IsActive", "SortOrder", "CreatedAt", "UpdatedAt" },
            columnTypes: new[] { "uniqueidentifier", "nvarchar(200)", "date", "nvarchar(1000)", "bit", "int", "datetime2", "datetime2" },
            values: new object[,]
            {
                { new Guid("5be8bf4a-b907-4cb7-b432-6f0ad744a601"), "Tết Nguyên Đán", new DateOnly(2026, 2, 17), "Ngày Tết cổ truyền Việt Nam.", true, 1, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("54734274-1761-4f89-bde4-aaf395f77227"), "Giỗ Tổ Hùng Vương", new DateOnly(2026, 4, 26), "Ngày tưởng nhớ các Vua Hùng.", true, 2, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("f220dc2d-3144-4e22-9cf7-d263a3a0ae24"), "30/4", new DateOnly(2026, 4, 30), "Ngày Giải phóng miền Nam, thống nhất đất nước.", true, 3, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("08fa50e1-4db4-43a8-8a46-5b2bb8558223"), "1/5", new DateOnly(2026, 5, 1), "Ngày Quốc tế Lao động.", true, 4, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("dcdba7be-423f-4bce-9610-1dd125ded071"), "2/9", new DateOnly(2026, 9, 2), "Ngày Quốc khánh nước Cộng hòa Xã hội Chủ nghĩa Việt Nam.", true, 5, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("921d0929-cf79-49f2-b39b-c4b6572c1162"), "Noel", new DateOnly(2026, 12, 25), "Ngày Lễ Giáng sinh.", true, 6, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) },
                { new Guid("307d161b-c945-4cc5-b02d-de2ea20e5f40"), "Tết Dương Lịch", new DateOnly(2026, 1, 1), "Ngày đầu năm Dương lịch.", true, 7, new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 14, 0, 0, 0, DateTimeKind.Utc) }
            });

        migrationBuilder.CreateIndex(
            name: "IX_NationalEvents_EventDate",
            table: "NationalEvents",
            column: "EventDate");

        migrationBuilder.CreateIndex(
            name: "IX_NationalEvents_IsActive",
            table: "NationalEvents",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_NationalEvents_SortOrder",
            table: "NationalEvents",
            column: "SortOrder");

        migrationBuilder.CreateIndex(
            name: "IX_News_Category",
            table: "News",
            column: "Category");

        migrationBuilder.CreateIndex(
            name: "IX_News_FeaturedOrder",
            table: "News",
            column: "FeaturedOrder");

        migrationBuilder.CreateIndex(
            name: "IX_News_IsFeatured",
            table: "News",
            column: "IsFeatured");

        migrationBuilder.CreateIndex(
            name: "IX_News_PublishedAt",
            table: "News",
            column: "PublishedAt");

        migrationBuilder.CreateIndex(
            name: "IX_News_Status",
            table: "News",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "UX_News_FeaturedOrder_Active",
            table: "News",
            column: "FeaturedOrder",
            unique: true,
            filter: "[IsFeatured] = 1 AND [FeaturedOrder] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NationalEvents");
        migrationBuilder.DropTable(name: "News");
    }
}

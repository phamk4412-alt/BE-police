using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PoliceBackend.Database;

#nullable disable

namespace PoliceBackend.Migrations;

[DbContext(typeof(IncidentDbContext))]
partial class IncidentDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "9.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        modelBuilder.Entity("PoliceBackend.Models.AuditLogRecord", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            b.Property<string>("Action").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<string>("ActorDisplayName").IsRequired().HasMaxLength(160).HasColumnType("nvarchar(160)");
            b.Property<string>("ActorRole").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
            b.Property<string>("ActorUsername").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("datetimeoffset");
            b.Property<string>("Detail").IsRequired().HasMaxLength(2000).HasColumnType("nvarchar(2000)");
            b.Property<string>("EntityId").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<string>("EntityType").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<string>("IpAddress").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("Summary").IsRequired().HasMaxLength(280).HasColumnType("nvarchar(280)");
            b.HasKey("Id");
            b.HasIndex("Action");
            b.HasIndex("ActorRole");
            b.HasIndex("CreatedAt");
            b.ToTable("AuditLogs");
        });

        modelBuilder.Entity("PoliceBackend.Models.IncidentRecord", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            b.Property<string>("Category").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<DateTimeOffset>("CreatedAt").HasColumnType("datetimeoffset");
            b.Property<string>("Detail").IsRequired().HasMaxLength(4000).HasColumnType("nvarchar(4000)");
            b.Property<string>("District").IsRequired().HasMaxLength(80).HasColumnType("nvarchar(80)");
            b.Property<string>("ImageUrls").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<string>("LastUpdatedBy").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<double>("Latitude").HasColumnType("float");
            b.Property<double>("Longitude").HasColumnType("float");
            b.Property<string>("ReporterName").IsRequired().HasMaxLength(120).HasColumnType("nvarchar(120)");
            b.Property<string>("ReporterPhone").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("Source").IsRequired().HasMaxLength(32).HasColumnType("nvarchar(32)");
            b.Property<string>("Status").IsRequired().HasMaxLength(64).HasColumnType("nvarchar(64)");
            b.Property<string>("TimeLabel").IsRequired().HasMaxLength(16).HasColumnType("nvarchar(16)");
            b.Property<string>("Title").IsRequired().HasMaxLength(160).HasColumnType("nvarchar(160)");
            b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("datetimeoffset");
            b.HasKey("Id");
            b.HasIndex("CreatedAt");
            b.HasIndex("District");
            b.HasIndex("Status");
            b.ToTable("Incidents");
        });

        modelBuilder.Entity("PoliceBackend.Models.NewsRecord", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uniqueidentifier");
            b.Property<string>("Category").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            b.Property<string>("Content").IsRequired().HasColumnType("nvarchar(max)");
            b.Property<DateTime>("CreatedAt").HasColumnType("datetime2");
            b.Property<string>("CreatedBy").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            b.Property<int?>("FeaturedOrder").HasColumnType("int");
            b.Property<bool>("IsFeatured").HasColumnType("bit");
            b.Property<DateTime?>("PublishedAt").HasColumnType("datetime2");
            b.Property<string>("Status").IsRequired().HasMaxLength(50).HasColumnType("nvarchar(50)");
            b.Property<string>("Summary").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<string>("ThumbnailUrl").IsRequired().HasMaxLength(1000).HasColumnType("nvarchar(1000)");
            b.Property<string>("Title").IsRequired().HasMaxLength(300).HasColumnType("nvarchar(300)");
            b.Property<DateTime>("UpdatedAt").HasColumnType("datetime2");
            b.Property<string>("UpdatedBy").IsRequired().HasMaxLength(100).HasColumnType("nvarchar(100)");
            b.HasKey("Id");
            b.HasIndex("Category");
            b.HasIndex("FeaturedOrder");
            b.HasIndex("IsFeatured");
            b.HasIndex("PublishedAt");
            b.HasIndex("Status");
            b.ToTable("News");
        });
    }
}

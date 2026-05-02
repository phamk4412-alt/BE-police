using Microsoft.EntityFrameworkCore;
using PoliceBackend.Models;

namespace PoliceBackend.Database;

public sealed class IncidentDbContext(DbContextOptions<IncidentDbContext> options) : DbContext(options)
{
    public DbSet<IncidentRecord> Incidents => Set<IncidentRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();
    public DbSet<AccountRecord> Accounts => Set<AccountRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<AccountRecord>();
        account.ToTable("Accounts");
        account.HasKey(item => item.Id);

        account.Property(item => item.Username)
            .HasMaxLength(120)
            .IsRequired();

        account.Property(item => item.NormalizedUsername)
            .HasMaxLength(120)
            .IsRequired();

        account.Property(item => item.DisplayName)
            .HasMaxLength(160)
            .IsRequired();

        account.Property(item => item.Role)
            .HasMaxLength(32)
            .IsRequired();

        account.Property(item => item.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        account.HasIndex(item => item.NormalizedUsername)
            .IsUnique();

        var incident = modelBuilder.Entity<IncidentRecord>();
        incident.ToTable("Incidents");
        incident.HasKey(item => item.Id);

        incident.Property(item => item.Title)
            .HasMaxLength(160)
            .IsRequired();

        incident.Property(item => item.Detail)
            .HasMaxLength(4000)
            .IsRequired();

        incident.Property(item => item.Category)
            .HasMaxLength(120)
            .IsRequired();

        incident.Property(item => item.Level)
            .HasMaxLength(24)
            .IsRequired();

        incident.Property(item => item.ClassificationReason)
            .HasMaxLength(500)
            .IsRequired();

        incident.Property(item => item.TimeLabel)
            .HasMaxLength(16)
            .IsRequired();

        incident.Property(item => item.District)
            .HasMaxLength(80)
            .IsRequired();

        incident.Property(item => item.Status)
            .HasMaxLength(64)
            .IsRequired();

        incident.Property(item => item.Source)
            .HasMaxLength(32)
            .IsRequired();

        incident.Property(item => item.ReporterName)
            .HasMaxLength(120)
            .IsRequired();

        incident.Property(item => item.LastUpdatedBy)
            .HasMaxLength(120)
            .IsRequired();

        incident.Property(item => item.InternalNote)
            .HasMaxLength(2000)
            .IsRequired();

        incident.HasIndex(item => item.CreatedAt);
        incident.HasIndex(item => item.Status);
        incident.HasIndex(item => item.Level);
        incident.HasIndex(item => item.District);

        var auditLog = modelBuilder.Entity<AuditLogRecord>();
        auditLog.ToTable("AuditLogs");
        auditLog.HasKey(item => item.Id);

        auditLog.Property(item => item.Action)
            .HasMaxLength(80)
            .IsRequired();

        auditLog.Property(item => item.EntityType)
            .HasMaxLength(80)
            .IsRequired();

        auditLog.Property(item => item.EntityId)
            .HasMaxLength(120)
            .IsRequired();

        auditLog.Property(item => item.ActorUsername)
            .HasMaxLength(120)
            .IsRequired();

        auditLog.Property(item => item.ActorDisplayName)
            .HasMaxLength(160)
            .IsRequired();

        auditLog.Property(item => item.ActorRole)
            .HasMaxLength(32)
            .IsRequired();

        auditLog.Property(item => item.Summary)
            .HasMaxLength(280)
            .IsRequired();

        auditLog.Property(item => item.Detail)
            .HasMaxLength(2000)
            .IsRequired();

        auditLog.Property(item => item.IpAddress)
            .HasMaxLength(64)
            .IsRequired();

        auditLog.HasIndex(item => item.CreatedAt);
        auditLog.HasIndex(item => item.Action);
        auditLog.HasIndex(item => item.ActorRole);
    }
}

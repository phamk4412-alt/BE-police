using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Services;

namespace PoliceBackend.Database;

public static class DatabaseBootstrapper
{
    public static async Task EnsureDatabaseReadyAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IncidentDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync();
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
        }

        await EnsureIncidentSchemaAsync(dbContext, configuration);
        await EnsureAccountProfileSchemaAsync(dbContext, configuration);
    }

    private static async Task EnsureIncidentSchemaAsync(IncidentDbContext dbContext, IConfiguration configuration)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var provider = DatabaseConfiguration.ResolveProvider(configuration);
        switch (provider)
        {
            case DatabaseProviders.SqlServer:
                await dbContext.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[Incidents]', N'U') IS NULL
BEGIN
    CREATE TABLE [Incidents] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(160) NOT NULL,
        [Detail] nvarchar(4000) NOT NULL,
        [Category] nvarchar(120) NOT NULL,
        [Level] nvarchar(24) NOT NULL,
        [UrgencyScore] int NOT NULL,
        [ClassificationReason] nvarchar(500) NOT NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [District] nvarchar(80) NOT NULL,
        [TimeLabel] nvarchar(16) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [Source] nvarchar(32) NOT NULL,
        [ReporterName] nvarchar(120) NOT NULL,
        [ReporterPhone] nvarchar(64) NOT NULL,
        [LastUpdatedBy] nvarchar(120) NOT NULL,
        [InternalNote] nvarchar(2000) NOT NULL,
        [ImageUrls] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'[Incidents]', N'ImageUrls') IS NULL
BEGIN
    ALTER TABLE [Incidents]
    ADD [ImageUrls] nvarchar(max) NOT NULL CONSTRAINT [DF_Incidents_ImageUrls] DEFAULT N'';
END;

IF COL_LENGTH(N'[Incidents]', N'ReporterPhone') IS NULL
BEGIN
    ALTER TABLE [Incidents]
    ADD [ReporterPhone] nvarchar(64) NOT NULL CONSTRAINT [DF_Incidents_ReporterPhone] DEFAULT N'';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_CreatedAt' AND object_id = OBJECT_ID(N'[Incidents]'))
    CREATE INDEX [IX_Incidents_CreatedAt] ON [Incidents] ([CreatedAt]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_Status' AND object_id = OBJECT_ID(N'[Incidents]'))
    CREATE INDEX [IX_Incidents_Status] ON [Incidents] ([Status]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_Level' AND object_id = OBJECT_ID(N'[Incidents]'))
    CREATE INDEX [IX_Incidents_Level] ON [Incidents] ([Level]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Incidents_District' AND object_id = OBJECT_ID(N'[Incidents]'))
    CREATE INDEX [IX_Incidents_District] ON [Incidents] ([District]);
""");
                break;

            case DatabaseProviders.Postgres:
                await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "Incidents" (
    "Id" uuid NOT NULL,
    "Title" character varying(160) NOT NULL,
    "Detail" character varying(4000) NOT NULL,
    "Category" character varying(120) NOT NULL,
    "Level" character varying(24) NOT NULL,
    "UrgencyScore" integer NOT NULL,
    "ClassificationReason" character varying(500) NOT NULL,
    "Latitude" double precision NOT NULL,
    "Longitude" double precision NOT NULL,
    "District" character varying(80) NOT NULL,
    "TimeLabel" character varying(16) NOT NULL,
    "Status" character varying(64) NOT NULL,
    "Source" character varying(32) NOT NULL,
    "ReporterName" character varying(120) NOT NULL,
    "ReporterPhone" character varying(64) NOT NULL,
    "LastUpdatedBy" character varying(120) NOT NULL,
    "InternalNote" character varying(2000) NOT NULL,
    "ImageUrls" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Incidents" PRIMARY KEY ("Id")
);

ALTER TABLE "Incidents"
ADD COLUMN IF NOT EXISTS "ImageUrls" text NOT NULL DEFAULT '';

ALTER TABLE "Incidents"
ADD COLUMN IF NOT EXISTS "ReporterPhone" character varying(64) NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS "IX_Incidents_CreatedAt" ON "Incidents" ("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_Incidents_Status" ON "Incidents" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Incidents_Level" ON "Incidents" ("Level");
CREATE INDEX IF NOT EXISTS "IX_Incidents_District" ON "Incidents" ("District");
""");
                break;
        }
    }

    private static async Task EnsureAccountProfileSchemaAsync(IncidentDbContext dbContext, IConfiguration configuration)
    {
        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var provider = DatabaseConfiguration.ResolveProvider(configuration);
        switch (provider)
        {
            case DatabaseProviders.SqlServer:
                await dbContext.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'[AccountProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [AccountProfiles] (
        [Id] uniqueidentifier NOT NULL,
        [ClerkUserId] nvarchar(120) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [DisplayName] nvarchar(160) NOT NULL,
        [Role] nvarchar(32) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CccdVerified] bit NOT NULL,
        [FaceScanned] bit NOT NULL,
        [DiditSessionId] nvarchar(160) NULL,
        [DiditStatus] nvarchar(64) NULL,
        [DiditApproved] bit NOT NULL,
        [DiditVerifiedAt] datetimeoffset NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_AccountProfiles] PRIMARY KEY ([Id])
    );
END;

IF COL_LENGTH(N'[AccountProfiles]', N'DiditSessionId') IS NULL
    ALTER TABLE [AccountProfiles] ADD [DiditSessionId] nvarchar(160) NULL;

IF COL_LENGTH(N'[AccountProfiles]', N'DiditStatus') IS NULL
    ALTER TABLE [AccountProfiles] ADD [DiditStatus] nvarchar(64) NULL;

IF COL_LENGTH(N'[AccountProfiles]', N'DiditApproved') IS NULL
    ALTER TABLE [AccountProfiles] ADD [DiditApproved] bit NOT NULL CONSTRAINT [DF_AccountProfiles_DiditApproved] DEFAULT CAST(0 AS bit);

IF COL_LENGTH(N'[AccountProfiles]', N'DiditVerifiedAt') IS NULL
    ALTER TABLE [AccountProfiles] ADD [DiditVerifiedAt] datetimeoffset NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AccountProfiles_ClerkUserId' AND object_id = OBJECT_ID(N'[AccountProfiles]'))
    CREATE UNIQUE INDEX [UX_AccountProfiles_ClerkUserId] ON [AccountProfiles] ([ClerkUserId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccountProfiles_Email' AND object_id = OBJECT_ID(N'[AccountProfiles]'))
    CREATE INDEX [IX_AccountProfiles_Email] ON [AccountProfiles] ([Email]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccountProfiles_Role' AND object_id = OBJECT_ID(N'[AccountProfiles]'))
    CREATE INDEX [IX_AccountProfiles_Role] ON [AccountProfiles] ([Role]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccountProfiles_DiditApproved' AND object_id = OBJECT_ID(N'[AccountProfiles]'))
    CREATE INDEX [IX_AccountProfiles_DiditApproved] ON [AccountProfiles] ([DiditApproved]);
""");
                break;

            case DatabaseProviders.Postgres:
                await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "AccountProfiles" (
    "Id" uuid NOT NULL,
    "ClerkUserId" character varying(120) NOT NULL,
    "Email" character varying(254) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "Role" character varying(32) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "CccdVerified" boolean NOT NULL,
    "FaceScanned" boolean NOT NULL,
    "DiditSessionId" character varying(160) NULL,
    "DiditStatus" character varying(64) NULL,
    "DiditApproved" boolean NOT NULL,
    "DiditVerifiedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_AccountProfiles" PRIMARY KEY ("Id")
);

ALTER TABLE "AccountProfiles" ADD COLUMN IF NOT EXISTS "DiditSessionId" character varying(160) NULL;
ALTER TABLE "AccountProfiles" ADD COLUMN IF NOT EXISTS "DiditStatus" character varying(64) NULL;
ALTER TABLE "AccountProfiles" ADD COLUMN IF NOT EXISTS "DiditApproved" boolean NOT NULL DEFAULT false;
ALTER TABLE "AccountProfiles" ADD COLUMN IF NOT EXISTS "DiditVerifiedAt" timestamp with time zone NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UX_AccountProfiles_ClerkUserId" ON "AccountProfiles" ("ClerkUserId");
CREATE INDEX IF NOT EXISTS "IX_AccountProfiles_Email" ON "AccountProfiles" ("Email");
CREATE INDEX IF NOT EXISTS "IX_AccountProfiles_Role" ON "AccountProfiles" ("Role");
CREATE INDEX IF NOT EXISTS "IX_AccountProfiles_DiditApproved" ON "AccountProfiles" ("DiditApproved");
""");
                break;
        }
    }

}

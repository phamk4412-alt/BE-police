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
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        await dbContext.Database.EnsureCreatedAsync();
        await EnsureAccountsTableAsync(dbContext, configuration);
        await EnsureIncidentSchemaAsync(dbContext, configuration);
        await authService.EnsureDemoAccountsAsync(dbContext);
    }

    private static async Task EnsureAccountsTableAsync(IncidentDbContext dbContext, IConfiguration configuration)
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
IF OBJECT_ID(N'[Accounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [Accounts] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(120) NOT NULL,
        [NormalizedUsername] nvarchar(120) NOT NULL,
        [DisplayName] nvarchar(160) NOT NULL,
        [Role] nvarchar(32) NOT NULL,
        [PasswordHash] nvarchar(512) NOT NULL,
        [IsDemoAccount] bit NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Accounts_NormalizedUsername' AND object_id = OBJECT_ID(N'[Accounts]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Accounts_NormalizedUsername] ON [Accounts] ([NormalizedUsername]);
END;
""");
                break;

            case DatabaseProviders.Postgres:
                await dbContext.Database.ExecuteSqlRawAsync("""
CREATE TABLE IF NOT EXISTS "Accounts" (
    "Id" uuid NOT NULL,
    "Username" character varying(120) NOT NULL,
    "NormalizedUsername" character varying(120) NOT NULL,
    "DisplayName" character varying(160) NOT NULL,
    "Role" character varying(32) NOT NULL,
    "PasswordHash" character varying(512) NOT NULL,
    "IsDemoAccount" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_NormalizedUsername" ON "Accounts" ("NormalizedUsername");
""");
                break;
        }
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
IF COL_LENGTH(N'[Incidents]', N'ImageUrls') IS NULL
BEGIN
    ALTER TABLE [Incidents]
    ADD [ImageUrls] nvarchar(max) NOT NULL CONSTRAINT [DF_Incidents_ImageUrls] DEFAULT N'';
END;
""");
                break;

            case DatabaseProviders.Postgres:
                await dbContext.Database.ExecuteSqlRawAsync("""
ALTER TABLE "Incidents"
ADD COLUMN IF NOT EXISTS "ImageUrls" text NOT NULL DEFAULT '';
""");
                break;
        }
    }
}

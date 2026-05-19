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

IF COL_LENGTH(N'[Incidents]', N'ReporterPhone') IS NULL
BEGIN
    ALTER TABLE [Incidents]
    ADD [ReporterPhone] nvarchar(64) NOT NULL CONSTRAINT [DF_Incidents_ReporterPhone] DEFAULT N'';
END;
""");
                break;

            case DatabaseProviders.Postgres:
                await dbContext.Database.ExecuteSqlRawAsync("""
ALTER TABLE "Incidents"
ADD COLUMN IF NOT EXISTS "ImageUrls" text NOT NULL DEFAULT '';

ALTER TABLE "Incidents"
ADD COLUMN IF NOT EXISTS "ReporterPhone" character varying(64) NOT NULL DEFAULT '';
""");
                break;
        }
    }

}

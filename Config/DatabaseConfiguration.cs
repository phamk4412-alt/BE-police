namespace PoliceBackend.Config;

public static class DatabaseConfiguration
{
    public static string ResolveProvider(IConfiguration configuration)
    {
        var configured = configuration["POLICE_DATABASE_PROVIDER"]
            ?? configuration["DatabaseProvider"];

        configured = configured?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (!string.IsNullOrWhiteSpace(ResolveConnectionString(configuration, "Postgres")))
        {
            return DatabaseProviders.Postgres;
        }

        if (!string.IsNullOrWhiteSpace(ResolveConnectionString(configuration, "SqlServer")))
        {
            return DatabaseProviders.SqlServer;
        }

        return DatabaseProviders.InMemory;
    }

    public static string? ResolveConnectionString(IConfiguration configuration, string name)
    {
        var envValue = configuration[$"POLICE_{name.ToUpperInvariant()}_CONNECTION"];
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Trim();
        }

        return configuration.GetConnectionString(name);
    }
}

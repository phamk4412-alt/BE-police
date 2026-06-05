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

        if (string.Equals(name, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var password = configuration["POLICE_SQLSERVER_PASSWORD"];
            if (!string.IsNullOrWhiteSpace(password))
            {
                var server = configuration["POLICE_SQLSERVER_SERVER"] ?? "161.248.147.174,10001";
                var database = configuration["POLICE_SQLSERVER_DATABASE"] ?? "police";
                var username = configuration["POLICE_SQLSERVER_USER"] ?? "sa";

                return
                    $"Server={server.Trim()};Database={database.Trim()};User Id={username.Trim()};Password={password};TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True";
            }
        }

        return configuration.GetConnectionString(name);
    }
}

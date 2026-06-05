namespace PoliceBackend.Config;

public static class DatabaseConfiguration
{
    public static string ResolveProvider(IConfiguration configuration)
    {
        var envProvider = NormalizeProvider(configuration["POLICE_DATABASE_PROVIDER"]);
        if (!string.IsNullOrWhiteSpace(envProvider))
        {
            return envProvider;
        }

        if (!string.IsNullOrWhiteSpace(configuration["DATABASE_URL"]))
        {
            return DatabaseProviders.Postgres;
        }

        if (!string.IsNullOrWhiteSpace(configuration["POLICE_POSTGRES_CONNECTION"]))
        {
            return DatabaseProviders.Postgres;
        }

        if (!string.IsNullOrWhiteSpace(configuration["POLICE_SQLSERVER_CONNECTION"]))
        {
            return DatabaseProviders.SqlServer;
        }

        var configuredProvider = NormalizeProvider(configuration["DatabaseProvider"]);
        if (!string.IsNullOrWhiteSpace(configuredProvider) &&
            configuredProvider != DatabaseProviders.InMemory)
        {
            return configuredProvider;
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

        if (name.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        {
            var databaseUrl = configuration["DATABASE_URL"];
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                return ConvertPostgresUrl(databaseUrl.Trim());
            }
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

    private static string? ConvertPostgresUrl(string databaseUrl)
    {
        if (!Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) ||
            !uri.Scheme.StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        {
            return databaseUrl;
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
        var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.IsDefaultPort ? 5432 : uri.Port;

        return string.Join(';', new[]
        {
            $"Host={uri.Host}",
            $"Port={port}",
            $"Database={Uri.UnescapeDataString(database)}",
            $"Username={username}",
            $"Password={password}",
            "SSL Mode=Require",
            "Trust Server Certificate=true"
        });
    }

    private static string? NormalizeProvider(string? provider) =>
        provider?.Trim().ToLowerInvariant();
}

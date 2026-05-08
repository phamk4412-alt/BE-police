namespace PoliceBackend.Config;

public static class AllowedOriginPolicy
{
    public static bool IsAllowedOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Equals("warteam.website", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("www.warteam.website", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }
}

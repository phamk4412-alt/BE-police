namespace PoliceBackend.Config;

// LM Studio exposes an OpenAI compatible API. The backend calls it so the
// https site never has to reach http://localhost from the browser.
public sealed class LmStudioSettings
{
    public const string DefaultBaseUrl = "http://localhost:1234/v1";

    public LmStudioSettings(IConfiguration configuration)
    {
        var baseUrl =
            configuration["LMSTUDIO_BASE_URL"] ??
            configuration["LmStudio:BaseUrl"] ??
            DefaultBaseUrl;

        BaseUrl = NormalizeBaseUrl(baseUrl);
        Model = (configuration["LMSTUDIO_MODEL"] ?? configuration["LmStudio:Model"] ?? string.Empty).Trim();
        ApiKey = (configuration["LMSTUDIO_API_KEY"] ?? configuration["LmStudio:ApiKey"] ?? string.Empty).Trim();
        TimeoutSeconds = Math.Clamp(configuration.GetValue("LMSTUDIO_TIMEOUT_SECONDS", 120), 10, 600);
        Temperature = Math.Clamp(configuration.GetValue("LMSTUDIO_TEMPERATURE", 0.6), 0, 2);
        MaxTokens = Math.Clamp(configuration.GetValue("LMSTUDIO_MAX_TOKENS", 700), 64, 4096);
        HistoryLimit = Math.Clamp(configuration.GetValue("LMSTUDIO_HISTORY", 10), 0, 40);
    }

    public string BaseUrl { get; }

    public bool IsLocalAddress =>
        Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));

    // Empty means: use the first model LM Studio has loaded.
    public string Model { get; }

    public string ApiKey { get; }

    public int TimeoutSeconds { get; }

    public double Temperature { get; }

    public int MaxTokens { get; }

    public int HistoryLimit { get; }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return DefaultBaseUrl;
        }

        return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? trimmed : $"{trimmed}/v1";
    }
}

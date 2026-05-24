using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class FacePlusPlusService
{
    private const string DefaultCompareEndpoint = "https://api-us.faceplusplus.com/facepp/v3/compare";
    private const double DefaultConfidenceThreshold = 73.975;
    private const int DefaultRequestTimeoutSeconds = 12;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public FacePlusPlusService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<FaceCompareResponse> CompareAsync(
        FaceCompareRequest request,
        CancellationToken cancellationToken)
    {
        var apiKey = ResolveConfigurationValue("FacePlusPlus:ApiKey", "FACEPP_API_KEY");
        var apiSecret = ResolveConfigurationValue("FacePlusPlus:ApiSecret", "FACEPP_API_SECRET");

        var cccdImage = NormalizeDataUrlBase64(request.CccdImage);
        var liveImage = NormalizeDataUrlBase64(request.LiveImage);

        if (string.IsNullOrWhiteSpace(cccdImage) || string.IsNullOrWhiteSpace(liveImage))
        {
            throw new InvalidOperationException("Thieu anh CCCD hoac anh khuon mat.");
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException("Face++ API key/secret chua duoc cau hinh.");
        }

        var endpoint = ResolveCompareEndpoint();

        return await CompareAtEndpointAsync(
            endpoint,
            apiKey,
            apiSecret,
            cccdImage,
            liveImage,
            cancellationToken);
    }

    public async Task<object> GetConfigurationStatusAsync(CancellationToken cancellationToken)
    {
        var apiKey = ResolveConfigurationValue("FacePlusPlus:ApiKey", "FACEPP_API_KEY");
        var apiSecret = ResolveConfigurationValue("FacePlusPlus:ApiSecret", "FACEPP_API_SECRET");
        var endpoint = ResolveCompareEndpoint();
        var status = "not_configured";
        string? facePlusPlusError = null;

        if (!string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret))
        {
            status = await CheckAuthenticationAsync(endpoint, apiKey, apiSecret, cancellationToken);
            facePlusPlusError = status == "authentication_failed" ? "AUTHENTICATION_ERROR" : null;
        }

        return new
        {
            Endpoint = endpoint,
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(apiKey),
            ApiSecretConfigured = !string.IsNullOrWhiteSpace(apiSecret),
            ApiKeyLength = apiKey?.Length ?? 0,
            ApiSecretLength = apiSecret?.Length ?? 0,
            ApiKeyFingerprint = CreateFingerprint(apiKey),
            ApiSecretFingerprint = CreateFingerprint(apiSecret),
            FacePlusPlusStatus = status,
            FacePlusPlusError = facePlusPlusError
        };
    }

    private async Task<FaceCompareResponse> CompareAtEndpointAsync(
        string endpoint,
        string apiKey,
        string apiSecret,
        string cccdImage,
        string liveImage,
        CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = _configuration.GetValue(
            "FacePlusPlus:RequestTimeoutSeconds",
            DefaultRequestTimeoutSeconds);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds)));

        using var content = new MultipartFormDataContent
        {
            { new StringContent(apiKey), "api_key" },
            { new StringContent(apiSecret), "api_secret" },
            { new StringContent(cccdImage), "image_base64_1" },
            { new StringContent(liveImage), "image_base64_2" }
        };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync(endpoint, content, timeoutTokenSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"Face++ compare timeout at {endpoint}.");
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(timeoutTokenSource.Token);

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            var hasApiError = root.TryGetProperty("error_message", out var errorElement);

            if (!response.IsSuccessStatusCode || hasApiError)
            {
                var errorMessage = errorElement.ValueKind == JsonValueKind.String
                    ? errorElement.GetString()
                    : response.ReasonPhrase;

                if (IsAuthenticationError(errorMessage))
                {
                    throw new InvalidOperationException(
                        $"Face++ xac thuc that bai tai {endpoint}. Kiem tra ApiKey/ApiSecret va dung endpoint cung vung voi tai khoan Face++.");
                }

                throw new InvalidOperationException($"Face++ compare failed at {endpoint}: {errorMessage ?? "unknown error"}");
            }

            var confidence = root.TryGetProperty("confidence", out var confidenceElement)
                ? confidenceElement.GetDouble()
                : 0;
            var threshold = ResolveThreshold(root);
            var requestId = root.TryGetProperty("request_id", out var requestIdElement)
                ? requestIdElement.GetString() ?? string.Empty
                : string.Empty;

            return new FaceCompareResponse(
                confidence >= threshold,
                Math.Round(confidence, 3),
                Math.Round(threshold, 3),
                requestId);
        }
    }

    private double ResolveThreshold(JsonElement root)
    {
        var configuredThreshold = ResolveConfiguredThreshold();

        if (configuredThreshold is > 0)
        {
            return configuredThreshold.Value;
        }

        if (root.TryGetProperty("thresholds", out var thresholds) &&
            thresholds.TryGetProperty("1e-5", out var strictThreshold) &&
            strictThreshold.TryGetDouble(out var threshold))
        {
            return threshold;
        }

        return DefaultConfidenceThreshold;
    }

    private double? ResolveConfiguredThreshold()
    {
        return _configuration.GetValue<double?>("FacePlusPlus:ConfidenceThreshold");
    }

    private async Task<string> CheckAuthenticationAsync(
        string endpoint,
        string apiKey,
        string apiSecret,
        CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = _configuration.GetValue(
            "FacePlusPlus:RequestTimeoutSeconds",
            DefaultRequestTimeoutSeconds);
        timeoutTokenSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds)));

        using var content = new MultipartFormDataContent
        {
            { new StringContent(apiKey), "api_key" },
            { new StringContent(apiSecret), "api_secret" }
        };

        try
        {
            using var response = await _httpClient.PostAsync(endpoint, content, timeoutTokenSource.Token);
            var responseBody = await response.Content.ReadAsStringAsync(timeoutTokenSource.Token);

            if (responseBody.Contains("AUTHENTICATION_ERROR", StringComparison.OrdinalIgnoreCase))
            {
                return "authentication_failed";
            }

            return "authentication_accepted";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "timeout";
        }
        catch (HttpRequestException)
        {
            return "network_error";
        }
    }

    private string ResolveCompareEndpoint()
    {
        var configuredEndpoint = _configuration["FacePlusPlus:CompareEndpoint"];

        return string.IsNullOrWhiteSpace(configuredEndpoint)
            ? DefaultCompareEndpoint
            : configuredEndpoint.Trim();
    }

    private string? ResolveConfigurationValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool IsAuthenticationError(string? errorMessage)
    {
        return errorMessage?.Contains("AUTHENTICATION_ERROR", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string? CreateFingerprint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));

        return Convert.ToHexString(hash)[..12];
    }

    public static string NormalizeDataUrlBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var commaIndex = value.IndexOf(',', StringComparison.Ordinal);
        var base64 = commaIndex >= 0 ? value[(commaIndex + 1)..] : value;

        return base64
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}

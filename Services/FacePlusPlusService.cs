using System.Text.Json;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class FacePlusPlusService
{
    private const string DefaultCompareEndpoint = "https://api-us.faceplusplus.com/facepp/v3/compare";
    private const string ChinaCompareEndpoint = "https://api-cn.faceplusplus.com/facepp/v3/compare";
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
        var apiKey = _configuration["FacePlusPlus:ApiKey"] ?? _configuration["FACEPP_API_KEY"];
        var apiSecret = _configuration["FacePlusPlus:ApiSecret"] ?? _configuration["FACEPP_API_SECRET"];

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

        var configuredEndpoint = _configuration["FacePlusPlus:CompareEndpoint"];
        var endpoints = string.IsNullOrWhiteSpace(configuredEndpoint)
            ? new[] { DefaultCompareEndpoint, ChinaCompareEndpoint }
            : new[] { configuredEndpoint };
        InvalidOperationException? lastException = null;

        foreach (var endpoint in endpoints)
        {
            try
            {
                return await CompareAtEndpointAsync(
                    endpoint,
                    apiKey,
                    apiSecret,
                    cccdImage,
                    liveImage,
                    cancellationToken);
            }
            catch (InvalidOperationException exception) when (
                endpoints.Length > 1 &&
                IsAuthenticationError(exception))
            {
                lastException = exception;
            }
        }

        throw lastException ?? new InvalidOperationException("Face++ compare failed.");
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

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = apiKey,
            ["api_secret"] = apiSecret,
            ["image_base64_1"] = cccdImage,
            ["image_base64_2"] = liveImage
        });

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

    private static bool IsAuthenticationError(InvalidOperationException exception)
    {
        return exception.Message.Contains("AUTHENTICATION_ERROR", StringComparison.OrdinalIgnoreCase);
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

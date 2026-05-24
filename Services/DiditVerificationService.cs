using System.Text;
using System.Text.Json;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class DiditVerificationService
{
    private const string DefaultBaseUrl = "https://verification.didit.me";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IdentityVerificationSessionService _identityVerificationSessionService;

    public DiditVerificationService(
        HttpClient httpClient,
        IConfiguration configuration,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _identityVerificationSessionService = identityVerificationSessionService;
    }

    public async Task<DiditSessionResponse> CreateSessionAsync(
        HttpContext context,
        CreateDiditSessionRequest request,
        CancellationToken cancellationToken)
    {
        var state = _identityVerificationSessionService.GetState(context);
        if (!state.CccdVerified)
        {
            throw new InvalidOperationException("Can xac thuc CCCD truoc khi quet khuon mat bang Didit.");
        }

        var apiKey = ResolveConfigurationValue("Didit:ApiKey", "DIDIT_API_KEY");
        var workflowId = ResolveConfigurationValue("Didit:WorkflowId", "DIDIT_WORKFLOW_ID");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(workflowId))
        {
            throw new InvalidOperationException("Didit API key/workflow id chua duoc cau hinh.");
        }

        var callbackUrl = NormalizeCallbackUrl(request.CallbackUrl);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{ResolveBaseUrl()}/v3/session/");
        message.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        message.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                workflow_id = workflowId,
                callback = callbackUrl,
                metadata = new
                {
                    source = "police-smart-hub-face-scan"
                }
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Didit create session failed: {ExtractDiditError(body) ?? response.ReasonPhrase}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var sessionId = GetString(root, "session_id", "id", "verification_session_id");
        var url = GetString(root, "url", "verification_url", "session_url");

        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("Didit create session response khong hop le.");
        }

        return new DiditSessionResponse(sessionId, url);
    }

    public async Task<DiditDecisionResponse> CompleteSessionAsync(
        HttpContext context,
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("Thieu Didit session id.");
        }

        var apiKey = ResolveConfigurationValue("Didit:ApiKey", "DIDIT_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Didit API key chua duoc cau hinh.");
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"{ResolveBaseUrl()}/v3/session/{Uri.EscapeDataString(sessionId)}/decision/");
        message.Headers.TryAddWithoutValidation("x-api-key", apiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Didit decision failed: {ExtractDiditError(body) ?? response.ReasonPhrase}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var status = ResolveDecisionStatus(root);
        var isApproved = IsApprovedStatus(status);
        var detail = isApproved ? "Didit approved" : status;

        if (isApproved)
        {
            _identityVerificationSessionService.SaveFace(context, new UpdateFaceVerificationRequest(
                null,
                true,
                false));
        }

        return new DiditDecisionResponse(sessionId, status, isApproved, detail);
    }

    private string ResolveBaseUrl()
    {
        var configured = ResolveConfigurationValue("Didit:BaseUrl", "DIDIT_BASE_URL");

        return string.IsNullOrWhiteSpace(configured)
            ? DefaultBaseUrl
            : configured.TrimEnd('/');
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

    private static string NormalizeCallbackUrl(string callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl) ||
            !Uri.TryCreate(callbackUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("CallbackUrl Didit khong hop le.");
        }

        return uri.ToString();
    }

    private static string ResolveDecisionStatus(JsonElement root)
    {
        var status = GetString(root, "status", "decision", "verification_status");

        if (!string.IsNullOrWhiteSpace(status))
        {
            return status;
        }

        if (root.TryGetProperty("decision", out var decision) && decision.ValueKind == JsonValueKind.Object)
        {
            status = GetString(decision, "status", "decision");
            if (!string.IsNullOrWhiteSpace(status))
            {
                return status;
            }
        }

        if (root.TryGetProperty("face_matches", out var faceMatches) &&
            faceMatches.ValueKind == JsonValueKind.Object)
        {
            status = GetString(faceMatches, "status", "decision");
            if (!string.IsNullOrWhiteSpace(status))
            {
                return status;
            }
        }

        return "unknown";
    }

    private static bool IsApprovedStatus(string status)
    {
        return status.Equals("approved", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("approve", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("accepted", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("success", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static string? ExtractDiditError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            return GetString(root, "message", "error", "detail");
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(body) ? null : body;
        }
    }
}

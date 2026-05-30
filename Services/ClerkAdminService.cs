using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PoliceBackend.Config;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class ClerkAdminService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ValidRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        AppRoles.Admin,
        AppRoles.Police,
        AppRoles.Support,
        AppRoles.User
    };

    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;

    public ClerkAdminService(HttpClient httpClient, IConfiguration configuration)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
    }

    public async Task<IReadOnlyCollection<ClerkAdminUserResponse>> GetUsersAsync(CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Get, "users?limit=100&order_by=-created_at", null, cancellationToken);
        var root = document.RootElement;
        var users = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

        return users.Select(user => MapUser(user)).ToArray();
    }

    public async Task<ClerkAdminUserResponse> GetUserAsync(string userId, CancellationToken cancellationToken)
    {
        using var document = await SendAsync(HttpMethod.Get, $"users/{Uri.EscapeDataString(userId)}", null, cancellationToken);
        return MapUser(document.RootElement, defaultMissingRole: false);
    }

    public async Task<ClerkAdminUserResponse> UpdateRoleAsync(
        string userId,
        string role,
        CancellationToken cancellationToken)
    {
        if (!ValidRoles.Contains(role))
        {
            throw new ArgumentException("Vai tro khong hop le.", nameof(role));
        }

        var payload = JsonSerializer.Serialize(new
        {
            public_metadata = new { role = role.ToLowerInvariant() },
            unsafe_metadata = new { role = (string?)null }
        }, JsonOptions);

        using var _ = await SendAsync(HttpMethod.Patch, $"users/{Uri.EscapeDataString(userId)}/metadata", payload, cancellationToken);
        using var document = await SendAsync(HttpMethod.Get, $"users/{Uri.EscapeDataString(userId)}", null, cancellationToken);

        return MapUser(document.RootElement);
    }

    public async Task<ClerkAdminUserResponse> UpdateStatusAsync(
        string userId,
        string status,
        CancellationToken cancellationToken)
    {
        var endpoint = status.Equals("locked", StringComparison.OrdinalIgnoreCase)
            ? "ban"
            : status.Equals("active", StringComparison.OrdinalIgnoreCase)
                ? "unban"
                : throw new ArgumentException("Trang thai khong hop le.", nameof(status));

        using var document = await SendAsync(
            HttpMethod.Post,
            $"users/{Uri.EscapeDataString(userId)}/{endpoint}",
            "{}",
            cancellationToken);

        return MapUser(document.RootElement);
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken)
    {
        await SendAsync(HttpMethod.Delete, $"users/{Uri.EscapeDataString(userId)}", null, cancellationToken);
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string endpoint,
        string? body,
        CancellationToken cancellationToken)
    {
        var secretKey =
            configuration["CLERK_SECRET_KEY"] ??
            configuration["CLERK_API_KEY"] ??
            configuration["Clerk:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Chua cau hinh CLERK_SECRET_KEY tren backend Render.");
        }

        using var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Clerk API loi {(int)response.StatusCode}: {content}");
        }

        return string.IsNullOrWhiteSpace(content)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(content);
    }

    private static ClerkAdminUserResponse MapUser(JsonElement user, bool defaultMissingRole = true)
    {
        var id = GetString(user, "id") ?? string.Empty;
        var firstName = GetString(user, "first_name");
        var lastName = GetString(user, "last_name");
        var username = GetString(user, "username");
        var name = string.Join(" ", new[] { firstName, lastName }.Where(item => !string.IsNullOrWhiteSpace(item))).Trim();
        var email = GetPrimaryEmail(user);
        var role = GetMetadataString(user, "public_metadata", "role")
            ?? GetMetadataString(user, "unsafe_metadata", "role")
            ?? (defaultMissingRole ? AppRoles.User : string.Empty);
        var status = GetBool(user, "banned") == true || GetBool(user, "locked") == true
            ? "locked"
            : "active";

        return new ClerkAdminUserResponse(
            id,
            string.IsNullOrWhiteSpace(name) ? username ?? email ?? id : name,
            email ?? string.Empty,
            role.ToLowerInvariant(),
            status,
            FromUnixMilliseconds(GetLong(user, "created_at")) ?? DateTimeOffset.UtcNow,
            FromUnixMilliseconds(GetLong(user, "last_sign_in_at")));
    }

    private static string? GetPrimaryEmail(JsonElement user)
    {
        var primaryEmailId = GetString(user, "primary_email_address_id");
        if (!user.TryGetProperty("email_addresses", out var emails) || emails.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var email in emails.EnumerateArray())
        {
            if (string.Equals(GetString(email, "id"), primaryEmailId, StringComparison.Ordinal))
            {
                return GetString(email, "email_address");
            }
        }

        return emails.EnumerateArray().Select(item => GetString(item, "email_address")).FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string? GetMetadataString(JsonElement user, string metadataName, string propertyName)
    {
        return user.TryGetProperty(metadataName, out var metadata) && metadata.ValueKind == JsonValueKind.Object
            ? GetString(metadata, propertyName)
            : null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result)
            ? result
            : null;
    }

    private static bool? GetBool(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
    }

    private static DateTimeOffset? FromUnixMilliseconds(long? value)
    {
        return value.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value) : null;
    }
}

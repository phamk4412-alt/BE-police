namespace PoliceBackend.Models;

public sealed class AccountProfileRecord
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = AccountRoles.User;
    public string Status { get; set; } = AccountStatuses.Active;
    public bool CccdVerified { get; set; }
    public bool FaceScanned { get; set; }
    public string? DiditSessionId { get; set; }
    public string? DiditStatus { get; set; }
    public bool DiditApproved { get; set; }
    public DateTimeOffset? DiditVerifiedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class AccountRoles
{
    public const string Admin = "admin";
    public const string Police = "police";
    public const string Support = "support";
    public const string User = "user";

    public static readonly string[] All = [Admin, Police, Support, User];

    public static string? Normalize(string? role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized is not null && All.Contains(normalized) ? normalized : null;
    }
}

public static class AccountStatuses
{
    public const string Active = "active";
    public const string Locked = "locked";

    public static string? Normalize(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            Active => Active,
            Locked => Locked,
            _ => null
        };
    }
}

public sealed record AccountProfileSyncRequest(
    bool? CccdVerified,
    bool? FaceScanned);

public sealed record AccountProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    bool CccdVerified,
    bool FaceScanned,
    string? DiditSessionId,
    string? DiditStatus,
    bool DiditApproved,
    DateTimeOffset? DiditVerifiedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminAccountResponse(
    string Id,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLogin);

public sealed record UpdateAccountRoleRequest(string Role);

public sealed record UpdateAccountStatusRequest(string Status);

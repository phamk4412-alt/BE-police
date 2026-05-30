namespace PoliceBackend.Models;

public sealed class AccountProfileRecord
{
    public Guid Id { get; set; }
    public string ClerkUserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string Status { get; set; } = "active";
    public bool CccdVerified { get; set; }
    public bool FaceScanned { get; set; }
    public string? DiditSessionId { get; set; }
    public string? DiditStatus { get; set; }
    public bool DiditApproved { get; set; }
    public DateTimeOffset? DiditVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed record ClerkAccountSnapshot(
    string? ClerkUserId,
    string? Email,
    string? DisplayName,
    string? Role,
    string? Status);

public sealed record AccountProfileSyncRequest(
    string? ClerkUserId,
    string? Email,
    string? DisplayName,
    string? Role,
    string? Status,
    bool? CccdVerified,
    bool? FaceScanned,
    string? DiditSessionId,
    string? DiditStatus,
    bool? DiditApproved);

public sealed record AccountProfileResponse(
    Guid Id,
    string ClerkUserId,
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

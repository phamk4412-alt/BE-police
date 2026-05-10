namespace PoliceBackend.Models;

public sealed record ClerkAdminUserResponse(
    string Id,
    string Name,
    string Email,
    string Role,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLogin,
    int? RelatedCases = null,
    int? SubmittedReports = null,
    string? Note = null);

public sealed record UpdateClerkUserRoleRequest(string Role);

public sealed record UpdateClerkUserStatusRequest(string Status);

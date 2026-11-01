namespace PoliceBackend.Models;

public sealed record GoogleLoginRequest(string? Credential);

public sealed record PasswordLoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(string? DisplayName, string? Email, string? Password);

public sealed record SelectRoleRequest(string? Role);

public sealed record CurrentUserResponse(
    string Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    string? AvatarUrl)
{
    // Every signed-in account may choose any of the four application roles.
    public string[] Roles => AccountRoles.All;
}

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    CurrentUserResponse User);

public readonly record struct ActorSnapshot(
    string Username,
    string DisplayName,
    string Role);

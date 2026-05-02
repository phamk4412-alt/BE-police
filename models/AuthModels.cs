using PoliceBackend.Config;

namespace PoliceBackend.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record RegisterRequest(
    string Username,
    string Password,
    string? DisplayName);

public sealed record AuthenticatedUserResponse(
    string Username,
    string DisplayName,
    string Role,
    string RedirectPath);

public readonly record struct DemoUser(
    string Username,
    string Password,
    string DisplayName,
    string Role);

public readonly record struct ActorSnapshot(
    string Username,
    string DisplayName,
    string Role);

public sealed record AdminAccountResponse(
    string Username,
    string DisplayName,
    string Role,
    bool IsDemoAccount);

public sealed class AccountRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string NormalizedUsername { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.User;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsDemoAccount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

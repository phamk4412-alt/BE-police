namespace PoliceBackend.Models;

public sealed record GoogleLoginRequest(string? Credential);

// Apple only sends the user's name on the first sign-in, and only to the app, so the app forwards it.
public sealed record AppleLoginRequest(string? IdentityToken, string? DisplayName);

public sealed record PasswordLoginRequest(string? Email, string? Password);

public sealed record RegisterRequest(string? DisplayName, string? Email, string? Password);

public sealed record CurrentUserResponse(
    string Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    string? AvatarUrl);

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    CurrentUserResponse User);

public readonly record struct ActorSnapshot(
    string Username,
    string DisplayName,
    string Role);

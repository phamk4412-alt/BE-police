namespace PoliceBackend.Models;

public sealed record GoogleLoginRequest(string? Credential);

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

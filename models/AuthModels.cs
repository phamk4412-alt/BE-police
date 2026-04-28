namespace PoliceBackend.Models;

public sealed record LoginRequest(string Username, string Password);

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

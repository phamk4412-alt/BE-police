namespace PoliceBackend.Models;

public sealed record AuthenticatedUserResponse(
    string Username,
    string DisplayName,
    string Role,
    string RedirectPath);

public readonly record struct ActorSnapshot(
    string Username,
    string DisplayName,
    string Role);

using System.Security.Claims;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class AuthService
{
    public AuthenticatedUserResponse? GetAuthenticatedUser(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(role))
        {
            return null;
        }

        return new AuthenticatedUserResponse(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            user.Identity?.Name ?? string.Empty,
            role,
            AuthRedirectUtils.GetLandingPathForRole(role));
    }

    public ActorSnapshot GetActorSnapshot(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return new ActorSnapshot("demo-user", "Nguoi dung demo", "Anonymous");
        }

        return new ActorSnapshot(
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            user.Identity?.Name ?? "Unknown user",
            user.FindFirstValue(ClaimTypes.Role) ?? "Unknown");
    }
}

using System.Security.Claims;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AuthService
{
    public Guid? GetAccountId(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
            ? accountId
            : null;
    }

    public ActorSnapshot GetActorSnapshot(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return new ActorSnapshot("demo-user", "Nguoi dung demo", "Anonymous");
        }

        return new ActorSnapshot(
            Truncate(user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", 120),
            Truncate(user.Identity?.Name ?? "Unknown user", 160),
            user.FindFirstValue(ClaimTypes.Role) ?? "Unknown");
    }

    public static ActorSnapshot GetActorSnapshot(AccountProfileRecord profile)
    {
        return new ActorSnapshot(
            Truncate(profile.Email, 120),
            Truncate(profile.DisplayName, 160),
            profile.Role);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

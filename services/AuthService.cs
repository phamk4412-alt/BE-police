using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using PoliceBackend.Config;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class AuthService
{
    private static readonly IReadOnlyDictionary<string, DemoUser> DemoUsers =
        new Dictionary<string, DemoUser>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"] = new("admin", "admin123", "Quan tri vien", AppRoles.Admin),
            ["admin2"] = new("admin2", "admin123", "Pho quan tri", AppRoles.Admin),
            ["user"] = new("user", "user123", "Nguoi dung", AppRoles.User),
            ["user2"] = new("user2", "user123", "Nguoi dan B", AppRoles.User),
            ["police"] = new("police", "police123", "Canh sat", AppRoles.Police),
            ["police2"] = new("police2", "police123", "Canh sat C5001", AppRoles.Police),
            ["c5001"] = new("c5001", "c5001", "Tran Nguyen Van A", AppRoles.Police),
            ["support"] = new("support", "support123", "Nhan vien ho tro", AppRoles.Support),
            ["support2"] = new("support2", "support123", "Nhan vien ho tro 2", AppRoles.Support)
        };

    public bool TryAuthenticate(string? username, string? password, out DemoUser user)
    {
        user = default;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var normalizedUsername = username.Trim().ToLowerInvariant();
        if (!DemoUsers.TryGetValue(normalizedUsername, out var candidate) ||
            !string.Equals(candidate.Password, password, StringComparison.Ordinal))
        {
            return false;
        }

        user = candidate;
        return true;
    }

    public ClaimsPrincipal CreatePrincipal(DemoUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Username),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    public AuthenticatedUserResponse CreateAuthenticatedResponse(DemoUser user)
    {
        return new AuthenticatedUserResponse(
            user.Username,
            user.DisplayName,
            user.Role,
            AuthRedirectUtils.GetLandingPathForRole(user.Role));
    }

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

    public IReadOnlyCollection<AdminAccountResponse> GetAccounts()
    {
        return DemoUsers.Values
            .OrderBy(item => item.Role)
            .ThenBy(item => item.Username)
            .Select(item => new AdminAccountResponse(
                item.Username,
                item.DisplayName,
                item.Role,
                true))
            .ToArray();
    }
}

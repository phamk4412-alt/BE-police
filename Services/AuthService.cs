using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class AuthService
{
    private const int PasswordHashIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

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

    public async Task EnsureDemoAccountsAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        foreach (var demoUser in DemoUsers.Values)
        {
            var normalizedUsername = NormalizeUsername(demoUser.Username);
            var accountExists = await dbContext.Accounts
                .AnyAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken);

            if (accountExists)
            {
                continue;
            }

            var now = DateTimeOffset.UtcNow;
            dbContext.Accounts.Add(new AccountRecord
            {
                Username = demoUser.Username,
                NormalizedUsername = normalizedUsername,
                DisplayName = demoUser.DisplayName,
                Role = demoUser.Role,
                PasswordHash = HashPassword(demoUser.Password),
                IsDemoAccount = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AccountRecord?> TryAuthenticateAsync(
        IncidentDbContext dbContext,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedUsername = NormalizeUsername(username);
        var account = await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken);

        if (account is null || !VerifyPassword(password, account.PasswordHash))
        {
            return null;
        }

        return account;
    }

    public async Task<(AccountRecord? Account, string? Error)> RegisterAsync(
        IncidentDbContext dbContext,
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return (null, "Ten dang nhap khong duoc de trong.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return (null, "Mat khau phai co it nhat 6 ky tu.");
        }

        var username = request.Username.Trim();
        if (username.Length > 120)
        {
            return (null, "Ten dang nhap khong duoc vuot qua 120 ky tu.");
        }

        var normalizedUsername = NormalizeUsername(username);
        var accountExists = await dbContext.Accounts
            .AnyAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken);

        if (accountExists)
        {
            return (null, "Ten dang nhap da ton tai.");
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? username
            : request.DisplayName.Trim();

        if (displayName.Length > 160)
        {
            return (null, "Ten hien thi khong duoc vuot qua 160 ky tu.");
        }

        var now = DateTimeOffset.UtcNow;
        var account = new AccountRecord
        {
            Username = username,
            NormalizedUsername = normalizedUsername,
            DisplayName = displayName,
            Role = AppRoles.User,
            PasswordHash = HashPassword(request.Password),
            IsDemoAccount = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Accounts.Add(account);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, "Ten dang nhap da ton tai.");
        }

        return (account, null);
    }

    public ClaimsPrincipal CreatePrincipal(AccountRecord user)
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

    public AuthenticatedUserResponse CreateAuthenticatedResponse(AccountRecord user)
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

    public async Task<IReadOnlyCollection<AdminAccountResponse>> GetAccountsAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(item => item.Role)
            .ThenBy(item => item.Username)
            .Select(item => new AdminAccountResponse(
                item.Username,
                item.DisplayName,
                item.Role,
                item.IsDemoAccount))
            .ToArrayAsync(cancellationToken);
    }

    public static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            "pbkdf2-sha256",
            PasswordHashIterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 ||
            !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expectedHash = Convert.FromBase64String(parts[3]);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

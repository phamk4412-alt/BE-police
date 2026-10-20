using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PoliceBackend.Config;

public sealed class AuthSettings
{
    public const string TokenIssuer = "police-smart-hub";
    public const string TokenAudience = "police-smart-hub";
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(7);

    private const int MinSecretLength = 32;

    public AuthSettings(IConfiguration configuration)
    {
        GoogleClientId =
            configuration["GOOGLE_CLIENT_ID"] ??
            configuration["Google:ClientId"] ??
            string.Empty;

        var secret = configuration["AUTH_JWT_SECRET"];
        HasConfiguredSecret = !string.IsNullOrWhiteSpace(secret) && secret.Length >= MinSecretLength;
        SigningKey = new SymmetricSecurityKey(
            HasConfiguredSecret
                ? Encoding.UTF8.GetBytes(secret!)
                : RandomNumberGenerator.GetBytes(64));

        AdminEmails = (configuration["POLICE_ADMIN_EMAILS"] ?? string.Empty)
            .Split([',', ';', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(email => email.ToLowerInvariant())
            .ToHashSet();
    }

    public string GoogleClientId { get; }

    public SymmetricSecurityKey SigningKey { get; }

    // Without AUTH_JWT_SECRET a random key is used, so every restart signs everyone out.
    public bool HasConfiguredSecret { get; }

    public IReadOnlySet<string> AdminEmails { get; }

    public bool IsBootstrapAdmin(string email) => AdminEmails.Contains(email.Trim().ToLowerInvariant());
}

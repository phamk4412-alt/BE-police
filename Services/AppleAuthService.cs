using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Config;

namespace PoliceBackend.Services;

public sealed record AppleIdentity(
    string Subject,
    string Email);

public sealed class AppleAuthService(AuthSettings authSettings)
{
    private const string Issuer = "https://appleid.apple.com";

    // Caches Apple's signing keys and refreshes them when Apple rotates them.
    private readonly ConfigurationManager<OpenIdConnectConfiguration> configurationManager = new(
        $"{Issuer}/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever { RequireHttps = true });

    private readonly JsonWebTokenHandler tokenHandler = new();

    public async Task<AppleIdentity> ValidateIdentityTokenAsync(string identityToken, CancellationToken cancellationToken)
    {
        var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);
        var result = await tokenHandler.ValidateTokenAsync(
            identityToken,
            new TokenValidationParameters
            {
                IssuerSigningKeys = configuration.SigningKeys,
                ValidAudiences = authSettings.AppleClientIds,
                ValidIssuer = Issuer
            });

        if (!result.IsValid)
        {
            throw new UnauthorizedAccessException("Token Apple khong hop le.", result.Exception);
        }

        var subject = result.ClaimsIdentity.FindFirst("sub")?.Value;
        var email = result.ClaimsIdentity.FindFirst("email")?.Value;
        var emailVerified = result.ClaimsIdentity.FindFirst("email_verified")?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new UnauthorizedAccessException("Token Apple khong hop le.");
        }

        // Apple sends email_verified as "true" (string) or true (bool) depending on the client.
        if (string.IsNullOrWhiteSpace(email) || !string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Tai khoan Apple chua chia se email.");
        }

        return new AppleIdentity(subject, email);
    }
}

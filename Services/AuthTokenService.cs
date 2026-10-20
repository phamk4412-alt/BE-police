using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Config;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AuthTokenService(AuthSettings authSettings)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AccountProfileRecord profile)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(AuthSettings.TokenLifetime);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AuthSettings.TokenIssuer,
            Audience = AuthSettings.TokenAudience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(authSettings.SigningKey, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, profile.Email)
            ])
        });

        return (token, expiresAt);
    }
}

using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Config;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AuthTokenService(AuthSettings authSettings)
{
    public const string SelectedRoleClaim = "selected_role";
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AccountProfileRecord profile, string? selectedRole = null)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, profile.Email)
        ]);
        if (selectedRole is not null)
        {
            var role = AccountRoles.Normalize(selectedRole)
                ?? throw new ArgumentException("Vai trò không hợp lệ.", nameof(selectedRole));
            identity.AddClaim(new Claim(SelectedRoleClaim, role));
        }
        var expiresAt = DateTimeOffset.UtcNow.Add(AuthSettings.TokenLifetime);
        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AuthSettings.TokenIssuer,
            Audience = AuthSettings.TokenAudience,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(authSettings.SigningKey, SecurityAlgorithms.HmacSha256),
            Subject = identity
        });

        return (token, expiresAt);
    }
}

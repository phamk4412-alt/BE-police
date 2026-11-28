using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PoliceBackend.Config;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AuthTokenService(AuthSettings authSettings, PoliceOnlineService policeOnline)
{
    public const string SelectedRoleClaim = "selected_role";
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AccountProfileRecord profile, string? selectedRole = null)
    {
        var tokenId = Guid.NewGuid().ToString("N");
        var identity = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, profile.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, tokenId),
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

        if (AccountRoles.Normalize(selectedRole ?? profile.Role) == AccountRoles.Police)
        {
            policeOnline.Start(profile.Id, tokenId, profile.DisplayName,
                DateTimeOffset.FromUnixTimeSeconds(expiresAt.ToUnixTimeSeconds()));
        }

        return (token, expiresAt);
    }
}

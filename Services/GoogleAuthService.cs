using Google.Apis.Auth;
using PoliceBackend.Config;

namespace PoliceBackend.Services;

public sealed record GoogleIdentity(
    string Subject,
    string Email,
    string? Name,
    string? Picture);

public sealed class GoogleAuthService(AuthSettings authSettings)
{
    public async Task<GoogleIdentity> ValidateIdTokenAsync(string idToken)
    {
        if (string.IsNullOrWhiteSpace(authSettings.GoogleClientId))
        {
            throw new InvalidOperationException("GOOGLE_CLIENT_ID chua duoc cau hinh tren backend.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [authSettings.GoogleClientId]
                });
        }
        catch (InvalidJwtException exception)
        {
            throw new UnauthorizedAccessException("Token Google khong hop le.", exception);
        }

        if (string.IsNullOrWhiteSpace(payload.Email) || !payload.EmailVerified)
        {
            throw new UnauthorizedAccessException("Tai khoan Google chua xac minh email.");
        }

        return new GoogleIdentity(payload.Subject, payload.Email, payload.Name, payload.Picture);
    }
}

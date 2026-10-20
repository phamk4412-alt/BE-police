using Microsoft.AspNetCore.Identity;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class PasswordService
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    private readonly PasswordHasher<AccountProfileRecord> _hasher = new();

    public string Hash(AccountProfileRecord profile, string password) => _hasher.HashPassword(profile, password);

    public bool Verify(AccountProfileRecord profile, string password)
    {
        if (string.IsNullOrEmpty(profile.PasswordHash))
        {
            return false;
        }

        return _hasher.VerifyHashedPassword(profile, profile.PasswordHash, password) != PasswordVerificationResult.Failed;
    }
}

using Microsoft.EntityFrameworkCore;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AccountProfileService
{
    public Task<AccountProfileRecord?> FindAsync(
        IncidentDbContext dbContext,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.AccountProfiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<AccountProfileRecord> UpsertGoogleAccountAsync(
        IncidentDbContext dbContext,
        GoogleIdentity identity,
        bool isBootstrapAdmin,
        CancellationToken cancellationToken = default)
    {
        return UpsertExternalAccountAsync(
            dbContext,
            $"google:{identity.Subject}",
            identity.Email,
            (profile, now) => ApplyGoogleIdentity(profile, identity, isBootstrapAdmin, now),
            cancellationToken);
    }

    public Task<AccountProfileRecord> UpsertAppleAccountAsync(
        IncidentDbContext dbContext,
        AppleIdentity identity,
        string? displayName,
        bool isBootstrapAdmin,
        CancellationToken cancellationToken = default)
    {
        return UpsertExternalAccountAsync(
            dbContext,
            $"apple:{identity.Subject}",
            identity.Email,
            (profile, now) => ApplyAppleIdentity(profile, identity, displayName, isBootstrapAdmin, now),
            cancellationToken);
    }

    private async Task<AccountProfileRecord> UpsertExternalAccountAsync(
        IncidentDbContext dbContext,
        string externalId,
        string email,
        Action<AccountProfileRecord, DateTimeOffset> applyIdentity,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var profile = await dbContext.AccountProfiles.FirstOrDefaultAsync(
            item => item.ExternalId == externalId,
            cancellationToken);

        // The provider has verified the email, so it may sign in to a password account registered with the same address.
        if (profile is null)
        {
            var localId = LocalExternalId(email);
            profile = await dbContext.AccountProfiles.FirstOrDefaultAsync(
                item => item.ExternalId == localId,
                cancellationToken);
        }

        var isNewProfile = profile is null;
        if (isNewProfile)
        {
            profile = new AccountProfileRecord
            {
                Id = Guid.NewGuid(),
                ExternalId = externalId,
                Role = AccountRoles.User,
                Status = AccountStatuses.Active,
                CreatedAt = now
            };
            dbContext.AccountProfiles.Add(profile);
        }

        applyIdentity(profile!, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewProfile)
        {
            // Another request created the same account concurrently; update that row instead.
            dbContext.ChangeTracker.Clear();
            profile = await dbContext.AccountProfiles.FirstOrDefaultAsync(
                item => item.ExternalId == externalId,
                cancellationToken);

            if (profile is null)
            {
                throw;
            }

            applyIdentity(profile, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return profile!;
    }

    public Task<AccountProfileRecord?> FindLocalAsync(
        IncidentDbContext dbContext,
        string email,
        CancellationToken cancellationToken = default)
    {
        var externalId = LocalExternalId(email);
        return dbContext.AccountProfiles.FirstOrDefaultAsync(item => item.ExternalId == externalId, cancellationToken);
    }

    // Returns null when the email already belongs to an account (password or Google).
    public async Task<AccountProfileRecord?> RegisterLocalAsync(
        IncidentDbContext dbContext,
        string displayName,
        string email,
        Func<AccountProfileRecord, string> hashPassword,
        bool isBootstrapAdmin,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var externalId = LocalExternalId(normalizedEmail);
        var emailTaken = await QueryAccounts(dbContext).AnyAsync(
            item => item.ExternalId == externalId || item.Email.ToLower() == normalizedEmail,
            cancellationToken);

        if (emailTaken)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var profile = new AccountProfileRecord
        {
            Id = Guid.NewGuid(),
            ExternalId = externalId,
            Email = TrimToMaxLength(normalizedEmail, 254),
            DisplayName = TrimToMaxLength(displayName, 160),
            Role = isBootstrapAdmin ? AccountRoles.Admin : AccountRoles.User,
            Status = AccountStatuses.Active,
            LastLoginAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        profile.PasswordHash = hashPassword(profile);
        dbContext.AccountProfiles.Add(profile);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique index on ExternalId: the same email registered concurrently.
            dbContext.ChangeTracker.Clear();
            return null;
        }

        return profile;
    }

    public async Task RecordLoginAsync(
        IncidentDbContext dbContext,
        AccountProfileRecord profile,
        bool isBootstrapAdmin,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (isBootstrapAdmin)
        {
            profile.Role = AccountRoles.Admin;
            profile.Status = AccountStatuses.Active;
        }

        profile.LastLoginAt = now;
        profile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string LocalExternalId(string email) => $"local:{NormalizeEmail(email)}";

    public async Task<AccountProfileResponse?> UpdateVerificationAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        bool? cccdVerified,
        bool? faceScanned,
        string? diditSessionId = null,
        string? diditStatus = null,
        bool? diditApproved = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindAsync(dbContext, accountId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;

        if (cccdVerified.HasValue)
        {
            profile.CccdVerified = cccdVerified.Value;
        }

        if (faceScanned.HasValue)
        {
            profile.FaceScanned = faceScanned.Value;
        }

        if (!string.IsNullOrWhiteSpace(diditSessionId))
        {
            profile.DiditSessionId = TrimToMaxLength(diditSessionId, 160);
        }

        if (!string.IsNullOrWhiteSpace(diditStatus))
        {
            profile.DiditStatus = TrimToMaxLength(diditStatus, 64);
        }

        if (diditApproved.HasValue)
        {
            profile.DiditApproved = diditApproved.Value;
            if (diditApproved.Value)
            {
                profile.DiditVerifiedAt ??= now;
            }
        }

        profile.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(profile);
    }

    public async Task<IReadOnlyCollection<AccountProfileResponse>> GetProfilesAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var profiles = await QueryAccounts(dbContext)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToResponse).ToArray();
    }

    public async Task<IReadOnlyCollection<AdminAccountResponse>> GetAdminAccountsAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var profiles = await QueryAccounts(dbContext)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToAdminResponse).ToArray();
    }

    public async Task<AdminAccountResponse?> UpdateRoleAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var normalizedRole = AccountRoles.Normalize(role)
            ?? throw new ArgumentException("Vai tro khong hop le.");

        var profile = await FindAsync(dbContext, accountId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.Role = normalizedRole;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToAdminResponse(profile);
    }

    public async Task<AdminAccountResponse?> UpdateStatusAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = AccountStatuses.Normalize(status)
            ?? throw new ArgumentException("Trang thai khong hop le.");

        var profile = await FindAsync(dbContext, accountId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        profile.Status = normalizedStatus;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToAdminResponse(profile);
    }

    public async Task<bool> DeleteAsync(
        IncidentDbContext dbContext,
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        var profile = await FindAsync(dbContext, accountId, cancellationToken);
        if (profile is null)
        {
            return false;
        }

        dbContext.AccountProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public static CurrentUserResponse ToCurrentUser(AccountProfileRecord profile)
    {
        return new CurrentUserResponse(
            profile.Id.ToString(),
            profile.Email,
            profile.DisplayName,
            profile.Role,
            profile.Status,
            profile.AvatarUrl);
    }

    // Older rows came from Clerk and can no longer sign in, so they are hidden from account lists.
    private static IQueryable<AccountProfileRecord> QueryAccounts(IncidentDbContext dbContext) =>
        dbContext.AccountProfiles
            .AsNoTracking()
            .Where(item =>
                item.ExternalId.StartsWith("google:") ||
                item.ExternalId.StartsWith("apple:") ||
                item.ExternalId.StartsWith("local:"));

    private static void ApplyGoogleIdentity(
        AccountProfileRecord profile,
        GoogleIdentity identity,
        bool isBootstrapAdmin,
        DateTimeOffset now)
    {
        profile.Email = TrimToMaxLength(identity.Email, 254);
        profile.DisplayName = TrimToMaxLength(
            string.IsNullOrWhiteSpace(identity.Name) ? identity.Email : identity.Name,
            160);
        profile.AvatarUrl = string.IsNullOrWhiteSpace(identity.Picture)
            ? null
            : TrimToMaxLength(identity.Picture, 512);

        if (isBootstrapAdmin)
        {
            profile.Role = AccountRoles.Admin;
            profile.Status = AccountStatuses.Active;
        }

        profile.LastLoginAt = now;
        profile.UpdatedAt = now;
    }

    private static void ApplyAppleIdentity(
        AccountProfileRecord profile,
        AppleIdentity identity,
        string? displayName,
        bool isBootstrapAdmin,
        DateTimeOffset now)
    {
        profile.Email = TrimToMaxLength(identity.Email, 254);

        // Apple sends the name only on the first sign-in; later sign-ins keep the stored one.
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            profile.DisplayName = TrimToMaxLength(displayName, 160);
        }
        else if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            profile.DisplayName = TrimToMaxLength(identity.Email, 160);
        }

        if (isBootstrapAdmin)
        {
            profile.Role = AccountRoles.Admin;
            profile.Status = AccountStatuses.Active;
        }

        profile.LastLoginAt = now;
        profile.UpdatedAt = now;
    }

    private static string TrimToMaxLength(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static AdminAccountResponse ToAdminResponse(AccountProfileRecord profile)
    {
        return new AdminAccountResponse(
            profile.Id.ToString(),
            profile.DisplayName,
            profile.Email,
            profile.Role,
            profile.Status,
            profile.CreatedAt,
            profile.LastLoginAt);
    }

    private static AccountProfileResponse ToResponse(AccountProfileRecord profile)
    {
        return new AccountProfileResponse(
            profile.Id,
            profile.Email,
            profile.DisplayName,
            profile.Role,
            profile.Status,
            profile.CccdVerified,
            profile.FaceScanned,
            profile.DiditSessionId,
            profile.DiditStatus,
            profile.DiditApproved,
            profile.DiditVerifiedAt,
            profile.LastLoginAt,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}

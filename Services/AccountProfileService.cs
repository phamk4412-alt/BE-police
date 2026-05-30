using Microsoft.EntityFrameworkCore;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AccountProfileService
{
    public async Task<AccountProfileResponse> SyncAsync(
        IncidentDbContext dbContext,
        AccountProfileSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClerkUserId))
        {
            throw new InvalidOperationException("Thieu ClerkUserId de dong bo tai khoan.");
        }

        var clerkUserId = request.ClerkUserId.Trim();
        var now = DateTimeOffset.UtcNow;
        var profile = await dbContext.AccountProfiles.FirstOrDefaultAsync(
            item => item.ClerkUserId == clerkUserId,
            cancellationToken);

        var isNewProfile = profile is null;
        if (isNewProfile)
        {
            profile = new AccountProfileRecord
            {
                Id = Guid.NewGuid(),
                ClerkUserId = clerkUserId,
                CreatedAt = now
            };
            dbContext.AccountProfiles.Add(profile);
        }

        Apply(profile!, request, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewProfile)
        {
            dbContext.ChangeTracker.Clear();
            profile = await dbContext.AccountProfiles.FirstOrDefaultAsync(
                item => item.ClerkUserId == clerkUserId,
                cancellationToken);

            if (profile is null)
            {
                throw;
            }

            Apply(profile, request, DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(profile!);
    }

    public Task<AccountProfileResponse> SyncClerkAsync(
        IncidentDbContext dbContext,
        ClerkAccountSnapshot? clerk,
        IdentityVerificationStateResponse state,
        string? diditSessionId,
        string? diditStatus,
        bool? diditApproved,
        CancellationToken cancellationToken = default)
    {
        if (clerk is null || string.IsNullOrWhiteSpace(clerk.ClerkUserId))
        {
            throw new InvalidOperationException("Thieu thong tin Clerk de dong bo tai khoan.");
        }

        return SyncAsync(
            dbContext,
            new AccountProfileSyncRequest(
                clerk.ClerkUserId,
                clerk.Email,
                clerk.DisplayName,
                clerk.Role,
                clerk.Status,
                state.CccdVerified,
                state.FaceScanned,
                diditSessionId,
                diditStatus,
                diditApproved),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccountProfileResponse>> GetProfilesAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.AccountProfiles
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToResponse).ToArray();
    }

    private static void Apply(
        AccountProfileRecord profile,
        AccountProfileSyncRequest request,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            profile.Email = TrimToMaxLength(request.Email, 254);
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
        {
            profile.DisplayName = TrimToMaxLength(request.DisplayName, 160);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            profile.Role = TrimToMaxLength(request.Role, 32).ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            profile.Status = TrimToMaxLength(request.Status, 32).ToLowerInvariant();
        }

        if (request.CccdVerified.HasValue)
        {
            profile.CccdVerified = request.CccdVerified.Value;
        }

        if (request.FaceScanned.HasValue)
        {
            profile.FaceScanned = request.FaceScanned.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.DiditSessionId))
        {
            profile.DiditSessionId = TrimToMaxLength(request.DiditSessionId, 160);
        }

        if (!string.IsNullOrWhiteSpace(request.DiditStatus))
        {
            profile.DiditStatus = TrimToMaxLength(request.DiditStatus, 64);
        }

        if (request.DiditApproved.HasValue)
        {
            profile.DiditApproved = request.DiditApproved.Value;
            if (request.DiditApproved.Value)
            {
                profile.DiditVerifiedAt ??= now;
            }
        }

        profile.UpdatedAt = now;
    }

    private static string TrimToMaxLength(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static AccountProfileResponse ToResponse(AccountProfileRecord profile)
    {
        return new AccountProfileResponse(
            profile.Id,
            profile.ClerkUserId,
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
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}

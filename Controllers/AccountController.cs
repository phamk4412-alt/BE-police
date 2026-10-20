using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class AccountController
{
    public static async Task<IResult> SyncAsync(
        HttpContext context,
        AccountProfileSyncRequest request,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        var accountId = authService.GetAccountId(context.User);
        if (accountId is null)
        {
            return Results.Unauthorized();
        }

        var profile = await accountProfileService.UpdateVerificationAsync(
            dbContext,
            accountId.Value,
            request.CccdVerified,
            request.FaceScanned,
            cancellationToken: cancellationToken);

        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }

    public static async Task<IResult> GetProfilesAsync(
        IncidentDbContext dbContext,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await accountProfileService.GetProfilesAsync(dbContext, cancellationToken));
    }
}

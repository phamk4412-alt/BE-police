using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class AccountController
{
    public static async Task<IResult> SyncAsync(
        AccountProfileSyncRequest request,
        IncidentDbContext dbContext,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await accountProfileService.SyncAsync(
                dbContext,
                request,
                cancellationToken));
        }
        catch (InvalidOperationException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
    }

    public static async Task<IResult> GetProfilesAsync(
        IncidentDbContext dbContext,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await accountProfileService.GetProfilesAsync(dbContext, cancellationToken));
    }
}

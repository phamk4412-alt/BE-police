using System.Text.Json;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class IdentityController
{
    public static IResult GetState(
        HttpContext context,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.GetState(context));
    }

    public static IResult SaveCccdState(
        HttpContext context,
        UpdateCccdVerificationRequest request,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.SaveCccd(context, request));
    }

    public static IResult SaveFaceState(
        HttpContext context,
        UpdateFaceVerificationRequest request,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        try
        {
            return Results.Ok(identityVerificationSessionService.SaveFace(context, request));
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Khong the cap nhat xac thuc khuon mat",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public static IResult ResetState(
        HttpContext context,
        IdentityVerificationSessionService identityVerificationSessionService)
    {
        return Results.Ok(identityVerificationSessionService.Reset(context));
    }

    public static async Task<IResult> CreateDiditSessionAsync(
        HttpContext context,
        CreateDiditSessionRequest request,
        IncidentDbContext dbContext,
        DiditVerificationService diditVerificationService,
        IdentityVerificationSessionService identityVerificationSessionService,
        AccountProfileService accountProfileService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await diditVerificationService.CreateSessionAsync(
                context,
                request,
                cancellationToken);

            await SyncVerificationAsync(
                context,
                dbContext,
                authService,
                accountProfileService,
                identityVerificationSessionService,
                session.SessionId,
                "created",
                false,
                cancellationToken);

            return Results.Ok(session);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Khong the tao phien Didit",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
    }

    public static async Task<IResult> CompleteDiditSessionAsync(
        HttpContext context,
        string sessionId,
        IncidentDbContext dbContext,
        DiditVerificationService diditVerificationService,
        IdentityVerificationSessionService identityVerificationSessionService,
        AccountProfileService accountProfileService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await diditVerificationService.CompleteSessionAsync(
                context,
                sessionId,
                cancellationToken);

            await SyncVerificationAsync(
                context,
                dbContext,
                authService,
                accountProfileService,
                identityVerificationSessionService,
                decision.SessionId,
                decision.Status,
                decision.IsApproved,
                cancellationToken);

            return Results.Ok(decision);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Khong the doc ket qua Didit",
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (JsonException)
        {
            return Results.Problem(
                title: "Khong the doc phan hoi Didit",
                detail: "Didit tra ve phan hoi khong hop le.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static async Task SyncVerificationAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        IdentityVerificationSessionService identityVerificationSessionService,
        string diditSessionId,
        string diditStatus,
        bool diditApproved,
        CancellationToken cancellationToken)
    {
        var accountId = authService.GetAccountId(context.User);
        if (accountId is null)
        {
            return;
        }

        var state = identityVerificationSessionService.GetState(context);
        try
        {
            await accountProfileService.UpdateVerificationAsync(
                dbContext,
                accountId.Value,
                state.CccdVerified,
                state.FaceScanned,
                diditSessionId,
                diditStatus,
                diditApproved,
                cancellationToken);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
        }
    }
}

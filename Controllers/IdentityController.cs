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
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await diditVerificationService.CreateSessionAsync(
                context,
                request,
                cancellationToken);

            if (request.Clerk is not null && !string.IsNullOrWhiteSpace(request.Clerk.ClerkUserId))
            {
                await accountProfileService.SyncClerkAsync(
                    dbContext,
                    request.Clerk,
                    identityVerificationSessionService.GetState(context),
                    session.SessionId,
                    "created",
                    false,
                    cancellationToken);
            }

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
        CompleteDiditSessionRequest? request,
        IncidentDbContext dbContext,
        DiditVerificationService diditVerificationService,
        IdentityVerificationSessionService identityVerificationSessionService,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        try
        {
            var decision = await diditVerificationService.CompleteSessionAsync(
                context,
                sessionId,
                cancellationToken);

            if (request?.Clerk is not null && !string.IsNullOrWhiteSpace(request.Clerk.ClerkUserId))
            {
                await accountProfileService.SyncClerkAsync(
                    dbContext,
                    request.Clerk,
                    identityVerificationSessionService.GetState(context),
                    decision.SessionId,
                    decision.Status,
                    decision.IsApproved,
                    cancellationToken);
            }

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
}

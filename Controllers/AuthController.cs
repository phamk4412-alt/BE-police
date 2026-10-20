using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class AuthController
{
    public static async Task<IResult> LoginWithGoogleAsync(
        HttpContext context,
        GoogleLoginRequest request,
        IncidentDbContext dbContext,
        AuthSettings authSettings,
        GoogleAuthService googleAuthService,
        AuthTokenService authTokenService,
        AccountProfileService accountProfileService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Credential))
        {
            return Results.BadRequest(new { message = "Thieu token Google." });
        }

        GoogleIdentity identity;
        try
        {
            identity = await googleAuthService.ValidateIdTokenAsync(request.Credential);
        }
        catch (UnauthorizedAccessException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var profile = await accountProfileService.UpsertGoogleAccountAsync(
            dbContext,
            identity,
            authSettings.IsBootstrapAdmin(identity.Email),
            cancellationToken);

        var actor = AuthService.GetActorSnapshot(profile);

        if (profile.Status != AccountStatuses.Active)
        {
            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.LoginFailed,
                entityType: AuditEntities.Auth,
                entityId: actor.Username,
                summary: "Dang nhap bi tu choi.",
                detail: $"{profile.DisplayName} dang nhap khi tai khoan dang bi khoa.",
                actor: actor,
                cancellationToken: cancellationToken);

            return Results.Json(new { message = "Tai khoan da bi khoa." }, statusCode: StatusCodes.Status403Forbidden);
        }

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.LoginSuccess,
            entityType: AuditEntities.Auth,
            entityId: actor.Username,
            summary: "Dang nhap bang Google.",
            detail: $"{profile.DisplayName} dang nhap bang Google.",
            actor: actor,
            cancellationToken: cancellationToken);

        var (token, expiresAt) = authTokenService.CreateToken(profile);
        return Results.Ok(new LoginResponse(token, expiresAt, AccountProfileService.ToCurrentUser(profile)));
    }

    public static async Task<IResult> LogoutAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var actor = authService.GetActorSnapshot(context.User);
            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.Logout,
                entityType: AuditEntities.Auth,
                entityId: actor.Username,
                summary: "Dang xuat.",
                detail: $"{actor.DisplayName} dang xuat khoi he thong.",
                actor: actor,
                cancellationToken: cancellationToken);
        }

        return Results.Ok(new { message = "Da dang xuat." });
    }

    public static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
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

        var profile = await accountProfileService.FindAsync(dbContext, accountId.Value, cancellationToken);
        return profile is null
            ? Results.Unauthorized()
            : Results.Ok(AccountProfileService.ToCurrentUser(profile));
    }
}

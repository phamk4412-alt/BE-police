using System.Net.Mail;
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

        return await CompleteLoginAsync(
            context,
            dbContext,
            profile,
            "Google",
            auditService,
            authTokenService,
            cancellationToken);
    }

    public static async Task<IResult> LoginWithAppleAsync(
        HttpContext context,
        AppleLoginRequest request,
        IncidentDbContext dbContext,
        AuthSettings authSettings,
        AppleAuthService appleAuthService,
        AuthTokenService authTokenService,
        AccountProfileService accountProfileService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            return Results.BadRequest(new { message = "Thieu token Apple." });
        }

        AppleIdentity identity;
        try
        {
            identity = await appleAuthService.ValidateIdentityTokenAsync(request.IdentityToken, cancellationToken);
        }
        catch (UnauthorizedAccessException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidOperationException)
        {
            return Results.Json(new { message = "Khong ket noi duoc may chu Apple." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var profile = await accountProfileService.UpsertAppleAccountAsync(
            dbContext,
            identity,
            request.DisplayName,
            authSettings.IsBootstrapAdmin(identity.Email),
            cancellationToken);

        return await CompleteLoginAsync(
            context,
            dbContext,
            profile,
            "Apple",
            auditService,
            authTokenService,
            cancellationToken);
    }

    public static async Task<IResult> LoginWithPasswordAsync(
        HttpContext context,
        PasswordLoginRequest request,
        IncidentDbContext dbContext,
        AuthSettings authSettings,
        PasswordService passwordService,
        AuthTokenService authTokenService,
        AccountProfileService accountProfileService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            return Results.BadRequest(new { message = "Vui lòng nhập email và mật khẩu." });
        }

        var profile = await accountProfileService.FindLocalAsync(dbContext, request.Email, cancellationToken);
        if (profile is null || !passwordService.Verify(profile, request.Password))
        {
            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.LoginFailed,
                entityType: AuditEntities.Auth,
                entityId: Truncate(AccountProfileService.NormalizeEmail(request.Email), 120),
                summary: "Dang nhap that bai.",
                detail: "Sai email hoac mat khau.",
                cancellationToken: cancellationToken);

            return Results.Json(new { message = "Email hoặc mật khẩu không đúng." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        await accountProfileService.RecordLoginAsync(
            dbContext,
            profile,
            authSettings.IsBootstrapAdmin(profile.Email),
            cancellationToken);

        return await CompleteLoginAsync(
            context,
            dbContext,
            profile,
            "mat khau",
            auditService,
            authTokenService,
            cancellationToken);
    }

    public static async Task<IResult> RegisterAsync(
        HttpContext context,
        RegisterRequest request,
        IncidentDbContext dbContext,
        AuthSettings authSettings,
        PasswordService passwordService,
        AuthTokenService authTokenService,
        AccountProfileService accountProfileService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (displayName.Length == 0)
        {
            return Results.BadRequest(new { message = "Vui lòng nhập họ tên." });
        }

        if (!MailAddress.TryCreate(email, out var mailAddress) || mailAddress.Address != email || email.Length > 254)
        {
            return Results.BadRequest(new { message = "Email không hợp lệ." });
        }

        if (password.Length < PasswordService.MinLength || password.Length > PasswordService.MaxLength)
        {
            return Results.BadRequest(new { message = $"Mật khẩu phải có từ {PasswordService.MinLength} đến {PasswordService.MaxLength} ký tự." });
        }

        var profile = await accountProfileService.RegisterLocalAsync(
            dbContext,
            displayName,
            email,
            account => passwordService.Hash(account, password),
            authSettings.IsBootstrapAdmin(email),
            cancellationToken);

        if (profile is null)
        {
            return Results.Conflict(new { message = "Email này đã được đăng ký. Hãy đăng nhập hoặc dùng Google." });
        }

        var actor = AuthService.GetActorSnapshot(profile);
        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.Register,
            entityType: AuditEntities.Auth,
            entityId: actor.Username,
            summary: "Dang ky tai khoan.",
            detail: $"{profile.DisplayName} tao tai khoan bang email.",
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

    public static async Task<IResult> DeleteCurrentUserAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        var accountId = authService.GetAccountId(context.User);
        if (accountId is null)
        {
            return Results.Unauthorized();
        }

        var actor = authService.GetActorSnapshot(context.User);
        if (!await accountProfileService.DeleteAsync(dbContext, accountId.Value, cancellationToken))
        {
            return Results.NotFound();
        }

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.DeleteOwnAccount,
            entityType: AuditEntities.Auth,
            entityId: actor.Username,
            summary: "Xoa tai khoan.",
            detail: $"{actor.DisplayName} tu xoa tai khoan cua minh.",
            actor: actor,
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> CompleteLoginAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AccountProfileRecord profile,
        string method,
        AuditService auditService,
        AuthTokenService authTokenService,
        CancellationToken cancellationToken)
    {
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

            return Results.Json(new { message = "Tài khoản đã bị khóa." }, statusCode: StatusCodes.Status403Forbidden);
        }

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.LoginSuccess,
            entityType: AuditEntities.Auth,
            entityId: actor.Username,
            summary: $"Dang nhap bang {method}.",
            detail: $"{profile.DisplayName} dang nhap bang {method}.",
            actor: actor,
            cancellationToken: cancellationToken);

        var (token, expiresAt) = authTokenService.CreateToken(profile);
        return Results.Ok(new LoginResponse(token, expiresAt, AccountProfileService.ToCurrentUser(profile)));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

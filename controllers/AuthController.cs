using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;

namespace PoliceBackend.Controllers;

public static class AuthController
{
    public static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AuditService auditService,
        CancellationToken cancellationToken)
    {
        if (!authService.TryAuthenticate(request.Username, request.Password, out var user))
        {
            var attemptedUsername = string.IsNullOrWhiteSpace(request.Username)
                ? "unknown"
                : request.Username.Trim();

            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.LoginFailed,
                entityType: AuditEntities.Auth,
                entityId: attemptedUsername,
                summary: "Dang nhap that bai.",
                detail: $"Tai khoan {attemptedUsername} dang nhap that bai.",
                actor: new ActorSnapshot(attemptedUsername, "Dang nhap that bai", "Unknown"),
                cancellationToken: cancellationToken);

            return Results.Unauthorized();
        }

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            authService.CreatePrincipal(user));

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.LoginSuccess,
            entityType: AuditEntities.Auth,
            entityId: user.Username,
            summary: "Dang nhap thanh cong.",
            detail: $"{user.DisplayName} dang nhap vao he thong voi vai tro {user.Role}.",
            actor: new ActorSnapshot(user.Username, user.DisplayName, user.Role),
            cancellationToken: cancellationToken);

        return Results.Ok(authService.CreateAuthenticatedResponse(user));
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

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { message = "Da dang xuat." });
    }

    public static IResult GetCurrentUser(ClaimsPrincipal user, AuthService authService)
    {
        var authenticatedUser = authService.GetAuthenticatedUser(user);
        return authenticatedUser is null
            ? Results.Unauthorized()
            : Results.Ok(authenticatedUser);
    }
}

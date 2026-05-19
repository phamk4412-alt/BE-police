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

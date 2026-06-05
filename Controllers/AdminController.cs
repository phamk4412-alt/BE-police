using System.Text;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using PoliceBackend.Utils;

namespace PoliceBackend.Controllers;

public static class AdminController
{
    public static async Task<IResult> GetAuditLogsAsync(
        IncidentDbContext dbContext,
        AuditService auditService,
        string? action,
        string? actorRole,
        string? entityType,
        int? limit,
        CancellationToken cancellationToken)
    {
        var logs = await auditService.GetLogsAsync(
            dbContext,
            action,
            actorRole,
            entityType,
            limit,
            cancellationToken);

        return Results.Ok(logs);
    }

    public static async Task<IResult> ExportIncidentsAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        string? search,
        string? status,
        string? source,
        string? district,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sort,
        CancellationToken cancellationToken)
    {
        var incidents = await incidentService.GetIncidentsAsync(
            dbContext,
            new(search, status, source, district, from, to, sort),
            cancellationToken);

        var actor = authService.GetActorSnapshot(context.User);
        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.ExportIncidents,
            entityType: AuditEntities.Incident,
            entityId: $"count:{incidents.Count}",
            summary: "Xuat bao cao vu viec.",
            detail: $"{actor.DisplayName} xuat {incidents.Count} dong du lieu bao cao.",
            actor: actor,
            cancellationToken: cancellationToken);

        return Results.File(
            Encoding.UTF8.GetBytes(IncidentCsvBuilder.Build(incidents)),
            "text/csv; charset=utf-8",
            $"incident-export-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    public static async Task<IResult> GetStatisticsAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetStatisticsAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> GetClerkAccountsAsync(
        IncidentDbContext dbContext,
        ClerkAdminService clerkAdminService,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        try
        {
            var users = await clerkAdminService.GetUsersAsync(cancellationToken);
            foreach (var user in users)
            {
                await accountProfileService.SyncAsync(
                    dbContext,
                    new AccountProfileSyncRequest(
                        user.Id,
                        user.Email,
                        user.Name,
                        user.Role,
                        user.Status,
                        null,
                        null,
                        null,
                        null,
                        null),
                    cancellationToken);
            }

            return Results.Ok(users);
        }
        catch (InvalidOperationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> UpdateClerkAccountRoleAsync(
        IncidentDbContext dbContext,
        ClerkAdminService clerkAdminService,
        AccountProfileService accountProfileService,
        string userId,
        UpdateClerkUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await clerkAdminService.UpdateRoleAsync(userId, request.Role, cancellationToken);
            await accountProfileService.SyncAsync(
                dbContext,
                new AccountProfileSyncRequest(
                    user.Id,
                    user.Email,
                    user.Name,
                    user.Role,
                    user.Status,
                    null,
                    null,
                    null,
                    null,
                    null),
                cancellationToken);

            return Results.Ok(user);
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> UpdateClerkAccountStatusAsync(
        IncidentDbContext dbContext,
        ClerkAdminService clerkAdminService,
        AccountProfileService accountProfileService,
        string userId,
        UpdateClerkUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await clerkAdminService.UpdateStatusAsync(userId, request.Status, cancellationToken);
            await accountProfileService.SyncAsync(
                dbContext,
                new AccountProfileSyncRequest(
                    user.Id,
                    user.Email,
                    user.Name,
                    user.Role,
                    user.Status,
                    null,
                    null,
                    null,
                    null,
                    null),
                cancellationToken);

            return Results.Ok(user);
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
        catch (InvalidOperationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> DeleteClerkAccountAsync(
        ClerkAdminService clerkAdminService,
        string userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await clerkAdminService.DeleteUserAsync(userId, cancellationToken);
            return Results.NoContent();
        }
        catch (InvalidOperationException error)
        {
            return Results.Json(new { message = error.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static IResult GetSystemHealth(IConfiguration configuration)
    {
        return SystemController.GetHealth(configuration);
    }
}

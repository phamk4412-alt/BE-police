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

    public static async Task<IResult> GetAccountsAsync(
        IncidentDbContext dbContext,
        AccountProfileService accountProfileService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await accountProfileService.GetAdminAccountsAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> UpdateAccountRoleAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        Guid userId,
        UpdateAccountRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (authService.GetAccountId(context.User) == userId)
        {
            return Results.BadRequest(new { message = "Khong the tu doi vai tro cua chinh minh." });
        }

        try
        {
            var account = await accountProfileService.UpdateRoleAsync(dbContext, userId, request.Role, cancellationToken);
            return account is null ? Results.NotFound() : Results.Ok(account);
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
    }

    public static async Task<IResult> UpdateAccountStatusAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        Guid userId,
        UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (authService.GetAccountId(context.User) == userId)
        {
            return Results.BadRequest(new { message = "Khong the tu khoa tai khoan cua chinh minh." });
        }

        try
        {
            var account = await accountProfileService.UpdateStatusAsync(dbContext, userId, request.Status, cancellationToken);
            return account is null ? Results.NotFound() : Results.Ok(account);
        }
        catch (ArgumentException error)
        {
            return Results.BadRequest(new { message = error.Message });
        }
    }

    public static async Task<IResult> DeleteAccountAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        AuthService authService,
        AccountProfileService accountProfileService,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (authService.GetAccountId(context.User) == userId)
        {
            return Results.BadRequest(new { message = "Khong the tu xoa tai khoan cua chinh minh." });
        }

        return await accountProfileService.DeleteAsync(dbContext, userId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }

    public static IResult GetSystemHealth(IConfiguration configuration)
    {
        return SystemController.GetHealth(configuration);
    }
}

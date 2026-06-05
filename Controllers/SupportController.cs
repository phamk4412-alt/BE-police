using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using PoliceBackend.Services.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace PoliceBackend.Controllers;

public static class SupportController
{
    public static async Task<IResult> GetIncidentsAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        string? status,
        CancellationToken cancellationToken)
    {
        var incidents = await incidentService.GetSupportIncidentsAsync(
            dbContext,
            status,
            cancellationToken);

        return Results.Ok(incidents);
    }

    public static async Task<IResult> UpdateIncidentStatusAsync(
        Guid id,
        SupportIncidentStatusUpdateRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var normalizedStatus = incidentService.NormalizeStatus(request.Status);

        if (context.User.Identity?.IsAuthenticated == true
            && !incidentService.CanUpdateIncidentStatus(actor.Role, normalizedStatus))
        {
            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.UpdateIncidentDenied,
                entityType: AuditEntities.Incident,
                entityId: id.ToString(),
                summary: "Bi tu choi cap nhat trang thai.",
                detail: $"{actor.DisplayName} khong du quyen cap nhat trang thai sang {normalizedStatus}.",
                actor: actor,
                cancellationToken: cancellationToken);

            return Results.Json(
                new { message = "Vai tro hien tai khong du quyen cap nhat trang thai nay." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await incidentService.UpdateIncidentStatusAsync(
            dbContext,
            hubContext,
            id,
            new UpdateIncidentStatusRequest(request.Status),
            actor,
            cancellationToken);

        if (result is null)
        {
            return Results.NotFound(new { message = "Khong tim thay vu viec." });
        }

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.UpdateIncidentStatus,
            entityType: AuditEntities.Incident,
            entityId: result.Incident.Id.ToString(),
            summary: "Cap nhat trang thai vu viec.",
            detail: $"{actor.DisplayName} cap nhat vu viec {result.Incident.Title} sang {result.Incident.Status}.",
            actor: actor,
            cancellationToken: cancellationToken);

        var updatedIncident = await incidentService.GetSupportIncidentByIdAsync(
            dbContext,
            id,
            cancellationToken);
        return updatedIncident is null
            ? Results.NotFound(new { message = "Khong tim thay vu viec." })
            : Results.Ok(updatedIncident);
    }

    public static async Task<IResult> DeleteIncidentAsync(
        Guid id,
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var actor = authService.GetActorSnapshot(context.User);
            var deletedIncident = await incidentService.DeleteIncidentAsync(
                dbContext,
                hubContext,
                id,
                cancellationToken);

            if (deletedIncident is null)
            {
                return Results.NotFound(new { message = "Khong tim thay vu viec." });
            }

            await auditService.WriteAsync(
                dbContext,
                context,
                action: AuditActions.DeleteIncident,
                entityType: AuditEntities.Incident,
                entityId: deletedIncident.Id.ToString(),
                summary: "Ho tro xoa vu viec.",
                detail: $"{actor.DisplayName} xoa vu viec {deletedIncident.Title} khoi database.",
                actor: actor,
                cancellationToken: cancellationToken);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Json(
                new { message = "Loi khi xoa vu viec khoi database.", error = ex.Message },
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public static async Task<IResult> GetDispatchBoardAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetDispatchBoardAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> GetCallIntakeAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        var dispatchBoard = await incidentService.GetDispatchBoardAsync(dbContext, cancellationToken);
        var intakeQueue = dispatchBoard
            .Where(item => item.Status is IncidentStatuses.MoiTiepNhan or IncidentStatuses.DangXacMinh)
            .Take(10)
            .ToArray();

        return Results.Ok(intakeQueue);
    }

    public static async Task<IResult> GetCenterOverviewAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetSupportCenterOverviewAsync(dbContext, cancellationToken));
    }
}

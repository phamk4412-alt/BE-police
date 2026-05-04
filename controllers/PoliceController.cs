using Microsoft.AspNetCore.SignalR;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using PoliceBackend.Services.Realtime;

namespace PoliceBackend.Controllers;

public static class PoliceController
{
    public static async Task<IResult> GetIncidentBoardAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        string? search,
        string? status,
        string? level,
        string? source,
        string? district,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? sort,
        CancellationToken cancellationToken)
    {
        var incidents = await incidentService.GetIncidentsAsync(
            dbContext,
            new IncidentQueryParameters(search, status, level, source, district, from, to, sort),
            cancellationToken);

        return Results.Ok(incidents);
    }

    public static async Task<IResult> UpdateIncidentStatusAsync(
        Guid id,
        UpdateIncidentStatusRequest request,
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

        if (!incidentService.CanUpdateIncidentStatus(actor.Role, normalizedStatus))
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
            request,
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

        return Results.Ok(result);
    }

    public static async Task<IResult> GetHotspotsAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetHotspotsAsync(dbContext, cancellationToken));
    }

    public static async Task<IResult> GetPatrolVehiclesAsync(
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        return Results.Ok(await incidentService.GetPatrolVehiclesAsync(dbContext, cancellationToken));
    }

    public static IResult GetActivePoliceLocations(
        PolicePresenceService policePresenceService)
    {
        return Results.Ok(policePresenceService.GetActiveLocations());
    }

    public static async Task<IResult> UpdateMyLocationAsync(
        PoliceLocationRequest request,
        HttpContext context,
        AuthService authService,
        PolicePresenceService policePresenceService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var (location, error) = policePresenceService.UpdateLocation(actor, request);

        if (location is null)
        {
            return Results.BadRequest(new { message = error });
        }

        await hubContext.Clients.All.SendAsync("PoliceLocationUpdated", location, cancellationToken);
        await hubContext.Clients.All.SendAsync(
            "PoliceLocationsSnapshot",
            policePresenceService.GetActiveLocations(),
            cancellationToken);

        return Results.Ok(location);
    }

    public static async Task<IResult> EndMyShiftAsync(
        EndPoliceShiftRequest? request,
        string? username,
        HttpContext context,
        AuthService authService,
        PolicePresenceService policePresenceService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var removed = policePresenceService.RemoveLocation(request?.Username ?? username)
            ?? policePresenceService.RemoveLocation(actor);

        if (removed is not null)
        {
            await hubContext.Clients.All.SendAsync("PoliceLocationRemoved", removed, cancellationToken);
            await hubContext.Clients.All.SendAsync(
                "PoliceLocationsSnapshot",
                policePresenceService.GetActiveLocations(),
                cancellationToken);
        }

        return Results.NoContent();
    }
}

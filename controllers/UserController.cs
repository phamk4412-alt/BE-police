using Microsoft.AspNetCore.SignalR;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services;
using PoliceBackend.Services.Realtime;

namespace PoliceBackend.Controllers;

public static class UserController
{
    public static async Task<IResult> AnalyzeIncidentAsync(
        AnalyzeIncidentRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        CancellationToken cancellationToken)
    {
        var assessment = incidentService.AnalyzeAssessment(request.Title, request.Detail, request.Level);
        var actor = authService.GetActorSnapshot(context.User);

        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.AnalyzeIncident,
            entityType: AuditEntities.Incident,
            entityId: "preview",
            summary: "Phan tich muc do khan cap.",
            detail: $"He thong phan tich yeu cau preview va danh gia {assessment.Category} - {assessment.Level}.",
            actor: actor,
            cancellationToken: cancellationToken);

        return Results.Ok(assessment.ToResponse());
    }

    public static async Task<IResult> CreateIncidentAsync(
        CreateIncidentRequest request,
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuditService auditService,
        AuthService authService,
        IHubContext<IncidentHub> hubContext,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        if (!incidentService.TryBuildIncident(request, actor, out var incident, out var assessment, out var error))
        {
            return Results.BadRequest(new { message = error });
        }

        dbContext.Incidents.Add(incident!);
        await auditService.WriteAsync(
            dbContext,
            context,
            action: AuditActions.CreateIncident,
            entityType: AuditEntities.Incident,
            entityId: incident!.Id.ToString(),
            summary: "Tao bao cao moi.",
            detail: $"{actor.DisplayName} tao bao cao {incident.Title} voi muc {incident.Level} ({incident.Category}).",
            actor: actor,
            saveChanges: false,
            cancellationToken: cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await incidentService.BroadcastCreatedAsync(hubContext, incident, cancellationToken);

        var response = new CreateIncidentResult(
            assessment!.ShouldCallEmergency
                ? "Da gui bao cao thanh cong. He thong danh gia day la tinh huong khan cap cao."
                : "Da gui bao cao thanh cong.",
            assessment.ToResponse(),
            incident.ToResponse());

        return Results.Ok(response);
    }

    public static async Task<IResult> GetIncidentByIdAsync(
        Guid id,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        var incident = await incidentService.GetIncidentByIdAsync(dbContext, id, cancellationToken);
        return incident is null
            ? Results.NotFound(new { message = "Khong tim thay vu viec." })
            : Results.Ok(incident);
    }

    public static async Task<IResult> GetReportHistoryAsync(
        HttpContext context,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        AuthService authService,
        int? limit,
        CancellationToken cancellationToken)
    {
        var actor = authService.GetActorSnapshot(context.User);
        var history = await incidentService.GetReportHistoryAsync(
            dbContext,
            actor,
            limit,
            cancellationToken);

        return Results.Ok(history);
    }

    public static async Task<IResult> GetNearbyAlertsAsync(
        string location,
        double? radiusKm,
        int? limit,
        IncidentDbContext dbContext,
        IncidentService incidentService,
        CancellationToken cancellationToken)
    {
        if (!incidentService.TryResolveLocation(location, out var resolvedLocation))
        {
            return Results.BadRequest(new { message = "Toa do khong hop le. Dung dinh dang '10.7769, 106.7009'." });
        }

        var alerts = await incidentService.GetNearbyAlertsAsync(
            dbContext,
            resolvedLocation!.Latitude,
            resolvedLocation.Longitude,
            Math.Clamp(radiusKm ?? 3, 0.5, 30),
            limit,
            cancellationToken);

        return Results.Ok(new
        {
            location = resolvedLocation,
            alerts
        });
    }

    public static IResult ResolveLocationAsync(
        LocationResolutionRequest request,
        IncidentService incidentService)
    {
        return incidentService.TryResolveLocation(request.Location, out var resolution)
            ? Results.Ok(resolution)
            : Results.BadRequest(new { message = "Toa do khong hop le. Dung dinh dang '10.7769, 106.7009'." });
    }
}

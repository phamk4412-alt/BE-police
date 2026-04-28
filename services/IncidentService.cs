using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PoliceBackend.Config;
using PoliceBackend.Database;
using PoliceBackend.Models;
using PoliceBackend.Services.Realtime;
using PoliceBackend.Utils;

namespace PoliceBackend.Services;

public sealed class IncidentService(IncidentAnalysisService analysisService)
{
    public IncidentAnalysisResponse Analyze(string? title, string? detail, string? requestedLevel)
    {
        return analysisService.Analyze(title, detail, requestedLevel).ToResponse();
    }

    public IncidentAssessment AnalyzeAssessment(string? title, string? detail, string? requestedLevel)
    {
        return analysisService.Analyze(title, detail, requestedLevel);
    }

    public async Task<IReadOnlyCollection<IncidentResponse>> GetIncidentsAsync(
        IncidentDbContext dbContext,
        IncidentQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .ApplyFilters(parameters, analysisService.NormalizeLevel, analysisService.NormalizeStatus)
            .ApplySort(parameters.Sort)
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => item.ToResponse())
            .ToArray();
    }

    public async Task<IncidentResponse?> GetIncidentByIdAsync(
        IncidentDbContext dbContext,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await dbContext.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == incidentId, cancellationToken);

        return incident?.ToResponse();
    }

    public bool TryBuildIncident(
        CreateIncidentRequest request,
        ActorSnapshot actor,
        out IncidentRecord? incident,
        out IncidentAssessment? assessment,
        out string? error)
    {
        incident = null;
        assessment = null;
        error = null;

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Location))
        {
            error = "Can co loai vu viec va toa do.";
            return false;
        }

        if (!GeoLocationUtils.TryParseLocation(request.Location, out var latitude, out var longitude))
        {
            error = "Toa do khong hop le. Dung dinh dang '10.7769, 106.7009'.";
            return false;
        }

        assessment = AnalyzeAssessment(request.Title, request.Detail, request.Level);
        var now = DateTimeOffset.UtcNow;

        incident = new IncidentRecord
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Detail = string.IsNullOrWhiteSpace(request.Detail)
                ? "Nguoi dung vua gui bao cao moi."
                : request.Detail.Trim(),
            Category = assessment.Category,
            Level = assessment.Level,
            UrgencyScore = assessment.UrgencyScore,
            ClassificationReason = assessment.Reason,
            Latitude = latitude,
            Longitude = longitude,
            District = GeoLocationUtils.ResolveDistrict(latitude, longitude),
            TimeLabel = DateTimeOffset.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Status = assessment.UrgencyScore >= 85
                ? IncidentStatuses.DangXacMinh
                : IncidentStatuses.MoiTiepNhan,
            Source = "user",
            ReporterName = actor.DisplayName,
            LastUpdatedBy = actor.DisplayName,
            InternalNote = assessment.Recommendation,
            CreatedAt = now,
            UpdatedAt = now
        };

        return true;
    }

    public async Task BroadcastCreatedAsync(
        IHubContext<IncidentHub> hubContext,
        IncidentRecord incident,
        CancellationToken cancellationToken = default)
    {
        await hubContext.Clients.All.SendAsync("IncidentCreated", incident.ToResponse(), cancellationToken);
    }

    public bool CanUpdateIncidentStatus(string role, string status)
    {
        return analysisService.CanUpdateIncidentStatus(role, status);
    }

    public string NormalizeStatus(string? status)
    {
        return analysisService.NormalizeStatus(status);
    }

    public async Task<UpdateIncidentStatusResult?> UpdateIncidentStatusAsync(
        IncidentDbContext dbContext,
        IHubContext<IncidentHub> hubContext,
        Guid incidentId,
        UpdateIncidentStatusRequest request,
        ActorSnapshot actor,
        CancellationToken cancellationToken = default)
    {
        var incident = await dbContext.Incidents.FirstOrDefaultAsync(
            item => item.Id == incidentId,
            cancellationToken);

        if (incident is null)
        {
            return null;
        }

        incident.Status = NormalizeStatus(request.Status);
        incident.LastUpdatedBy = actor.DisplayName;
        incident.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.InternalNote))
        {
            incident.InternalNote = request.InternalNote.Trim();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var payload = incident.ToResponse();
        await hubContext.Clients.All.SendAsync("IncidentUpdated", payload, cancellationToken);

        return new UpdateIncidentStatusResult("Da cap nhat trang thai.", payload);
    }

    public async Task<IReadOnlyCollection<IncidentResponse>> GetReportHistoryAsync(
        IncidentDbContext dbContext,
        ActorSnapshot actor,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit ?? 25, 1, 100);

        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Where(item => item.ReporterName == actor.DisplayName)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => item.ToResponse())
            .ToArray();
    }

    public bool TryResolveLocation(string rawLocation, out LocationResolutionResponse? response)
    {
        response = null;
        if (!GeoLocationUtils.TryParseLocation(rawLocation, out var latitude, out var longitude))
        {
            return false;
        }

        response = new LocationResolutionResponse(
            latitude,
            longitude,
            GeoLocationUtils.ResolveDistrict(latitude, longitude),
            GeoLocationUtils.IsWithinCoverage(latitude, longitude));

        return true;
    }

    public async Task<IReadOnlyCollection<NearbyAlertResponse>> GetNearbyAlertsAsync(
        IncidentDbContext dbContext,
        double latitude,
        double longitude,
        double radiusKm,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var take = Math.Clamp(limit ?? 20, 1, 100);

        return incidents
            .Select(item => new NearbyAlertResponse(
                item.Id,
                item.Title,
                item.Category,
                item.Level,
                item.Status,
                item.District,
                item.Latitude,
                item.Longitude,
                Math.Round(GeoLocationUtils.CalculateDistanceKm(latitude, longitude, item.Latitude, item.Longitude), 2),
                item.CreatedAt))
            .Where(item => item.DistanceKm <= radiusKm)
            .OrderBy(item => item.DistanceKm)
            .ThenByDescending(item => item.CreatedAt)
            .Take(take)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<HotspotResponse>> GetHotspotsAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Where(item => !string.Equals(item.Status, IncidentStatuses.DaXuLy, StringComparison.OrdinalIgnoreCase))
            .ToListAsync(cancellationToken);

        return incidents
            .GroupBy(item => item.District)
            .Select(group => new HotspotResponse(
                group.Key,
                group.Count(),
                group.Count(item => item.UrgencyScore >= 85),
                Math.Round(group.Average(item => item.UrgencyScore), 1),
                group.Count(item => item.UrgencyScore >= 85) > 0
                    ? "Tang cuong tuan tra va uu tien xu ly nhom nguy co cao."
                    : "Duy tri giam sat va bo tri don vi co dong khi can."))
            .OrderByDescending(item => item.HighUrgencyCount)
            .ThenByDescending(item => item.OpenIncidentCount)
            .Take(10)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PatrolVehicleResponse>> GetPatrolVehiclesAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var hotspots = await GetHotspotsAsync(dbContext, cancellationToken);
        if (hotspots.Count == 0)
        {
            return
            [
                new PatrolVehicleResponse(
                    "PX-01",
                    "TP.HCM",
                    "San sang",
                    0,
                    10,
                    "Duy tri tuan tra co ban tai khu vuc trung tam.")
            ];
        }

        return hotspots
            .Select((hotspot, index) => new PatrolVehicleResponse(
                $"PX-{index + 1:00}",
                hotspot.District,
                hotspot.HighUrgencyCount > 0 ? "Dang co dong" : "Dang tuan tra",
                hotspot.OpenIncidentCount,
                (hotspot.HighUrgencyCount * 10) + (int)Math.Round(hotspot.AverageUrgencyScore),
                hotspot.RecommendedAction))
            .Take(5)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DispatchQueueItemResponse>> GetDispatchBoardAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .Where(item => !string.Equals(item.Status, IncidentStatuses.DaXuLy, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.UrgencyScore)
            .ThenByDescending(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => new DispatchQueueItemResponse(
                item.Id,
                item.Title,
                item.Status,
                item.Level,
                item.UrgencyScore,
                item.District,
                item.ReporterName,
                item.CreatedAt,
                item.UrgencyScore >= 85
                    ? "Dieu dong to co dong va xac minh hien truong ngay."
                    : item.UrgencyScore >= 60
                        ? "Lien he tong dai va bo tri can bo ho tro."
                        : "Theo doi, bo sung thong tin va xep hang doi xu ly."))
            .ToArray();
    }

    public async Task<SupportCenterOverviewResponse> GetSupportCenterOverviewAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var dispatchBoard = await GetDispatchBoardAsync(dbContext, cancellationToken);
        var hotspots = await GetHotspotsAsync(dbContext, cancellationToken);

        return new SupportCenterOverviewResponse(
            PendingCalls: dispatchBoard.Count(item => item.Status == IncidentStatuses.MoiTiepNhan),
            ActiveDispatches: dispatchBoard.Count(item =>
                item.Status == IncidentStatuses.DangXacMinh || item.Status == IncidentStatuses.DaDieuPhoi),
            HighPriorityIncidents: dispatchBoard.Count(item => item.UrgencyScore >= 85),
            Hotspots: hotspots);
    }

    public async Task<AdminStatisticsResponse> GetStatisticsAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var auditLogCount = await dbContext.AuditLogs.CountAsync(cancellationToken);

        var byStatus = incidents
            .GroupBy(item => item.Status)
            .Select(group => new MetricCountResponse(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ToArray();

        var byLevel = incidents
            .GroupBy(item => item.Level)
            .Select(group => new MetricCountResponse(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .ToArray();

        var byDistrict = incidents
            .GroupBy(item => item.District)
            .Select(group => new MetricCountResponse(group.Key, group.Count()))
            .OrderByDescending(item => item.Count)
            .Take(10)
            .ToArray();

        return new AdminStatisticsResponse(
            TotalIncidents: incidents.Count,
            OpenIncidents: incidents.Count(item => !string.Equals(item.Status, IncidentStatuses.DaXuLy, StringComparison.OrdinalIgnoreCase)),
            ResolvedIncidents: incidents.Count(item => string.Equals(item.Status, IncidentStatuses.DaXuLy, StringComparison.OrdinalIgnoreCase)),
            HighUrgencyIncidents: incidents.Count(item => item.UrgencyScore >= 85),
            ByStatus: byStatus,
            ByLevel: byLevel,
            ByDistrict: byDistrict,
            AuditLogCount: auditLogCount);
    }
}

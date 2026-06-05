using System.Globalization;
using Microsoft.AspNetCore.Http;
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
    public IncidentAnalysisResponse Analyze(string? title, string? detail)
    {
        return analysisService.Analyze(title, detail).ToResponse();
    }

    public IncidentAssessment AnalyzeAssessment(string? title, string? detail)
    {
        return analysisService.Analyze(title, detail);
    }

    public async Task<IReadOnlyCollection<IncidentResponse>> GetIncidentsAsync(
        IncidentDbContext dbContext,
        IncidentQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .ApplyFilters(parameters, analysisService.NormalizeStatus)
            .ApplySort(parameters.Sort)
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => item.ToResponse())
            .ToArray();
    }

    public async Task<IReadOnlyCollection<SupportIncidentResponse>> GetSupportIncidentsAsync(
        IncidentDbContext dbContext,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .ApplyFilters(
                new IncidentQueryParameters(null, status, null, null, null, null, "created_desc"),
                analysisService.NormalizeStatus)
            .ApplySort("created_desc")
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => item.ToSupportResponse())
            .ToArray();
    }

    public async Task<SupportIncidentResponse?> GetSupportIncidentByIdAsync(
        IncidentDbContext dbContext,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await dbContext.Incidents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return null;
        }

        return incident.ToSupportResponse();
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

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            error = "Can co tieu de vu viec.";
            return false;
        }

        if (!GeoLocationUtils.IsWithinCoverage(request.Latitude, request.Longitude))
        {
            error = "Toa do khong hop le hoac nam ngoai khu vuc ho tro.";
            return false;
        }

        assessment = AnalyzeAssessment(request.Title, request.Detail);
        var now = DateTimeOffset.UtcNow;
        var category = ResolveCategory(request, assessment);

        incident = new IncidentRecord
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Detail = request.Detail?.Trim() ?? string.Empty,
            Category = category,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            District = GeoLocationUtils.ResolveDistrict(request.Latitude, request.Longitude),
            TimeLabel = DateTimeOffset.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Status = assessment.ShouldCallEmergency
                ? IncidentStatuses.DangXacMinh
                : IncidentStatuses.MoiTiepNhan,
            Source = "user",
            ReporterName = actor.DisplayName,
            ReporterPhone = actor.Username,
            LastUpdatedBy = actor.DisplayName,
            CreatedAt = now,
            UpdatedAt = now
        };

        return true;
    }

    private static string ResolveCategory(CreateIncidentRequest request, IncidentAssessment assessment)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return assessment.Category;
        }

        var category = request.Category.Trim();
        if (!IsOtherCategory(category))
        {
            return category;
        }

        return string.IsNullOrWhiteSpace(request.CustomCategory)
            ? assessment.Category
            : request.CustomCategory.Trim();
    }

    private static bool IsOtherCategory(string category)
    {
        return string.Equals(
            TextNormalizationUtils.RemoveDiacritics(category).Trim(),
            "Khac",
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryValidateImages(
        IReadOnlyCollection<IFormFile>? images,
        out string? error)
    {
        error = null;

        if (images is null || images.Count == 0)
        {
            return true;
        }

        if (images.Count > 3)
        {
            error = "Too many images. Chi duoc tai toi da 3 anh.";
            return false;
        }

        foreach (var image in images)
        {
            if (image.Length == 0)
            {
                error = "Tap tin anh rong.";
                return false;
            }

            if (image.Length > 5 * 1024 * 1024)
            {
                error = "Moi anh phai nho hon 5MB.";
                return false;
            }
        }

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

        await dbContext.SaveChangesAsync(cancellationToken);
        var payload = incident.ToResponse();
        await hubContext.Clients.All.SendAsync("IncidentUpdated", payload, cancellationToken);

        return new UpdateIncidentStatusResult("Da cap nhat trang thai.", payload);
    }

    public async Task<IncidentResponse?> DeleteIncidentAsync(
        IncidentDbContext dbContext,
        IHubContext<IncidentHub> hubContext,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await dbContext.Incidents.FirstOrDefaultAsync(
            item => item.Id == incidentId,
            cancellationToken);

        if (incident is null)
        {
            return null;
        }

        try
        {
            var payload = incident.ToResponse();
            
            // Delete the incident from database
            dbContext.Incidents.Remove(incident);
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Detach the entity to avoid tracking issues
            dbContext.Entry(incident).State = EntityState.Detached;
            
            // Send SignalR notification after successful database deletion
            await hubContext.Clients.All.SendAsync("IncidentDeleted", new { id = incidentId }, cancellationToken);

            return payload;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Loi khi xoa vu viec {incidentId}: {ex.Message}", ex);
        }
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
                group.Any(item => item.Status == IncidentStatuses.MoiTiepNhan)
                    ? "Tang cuong tiep nhan, xac minh cac bao cao moi."
                    : "Duy tri giam sat va bo tri don vi co dong khi can."))
            .OrderByDescending(item => item.OpenIncidentCount)
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
                hotspot.OpenIncidentCount > 0 ? "Dang co dong" : "Dang tuan tra",
                hotspot.OpenIncidentCount,
                hotspot.OpenIncidentCount * 10,
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
            .OrderByDescending(item => item.CreatedAt)
            .Take(25)
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => new DispatchQueueItemResponse(
                item.Id,
                item.Title,
                item.Status,
                item.District,
                item.ReporterName,
                item.CreatedAt,
                item.Status == IncidentStatuses.MoiTiepNhan
                    ? "Lien he nguoi bao cao va xac minh thong tin."
                    : "Theo doi tien do xu ly va cap nhat trang thai."))
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
            ActiveIncidentCount: dispatchBoard.Count,
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
            ByStatus: byStatus,
            ByDistrict: byDistrict,
            AuditLogCount: auditLogCount);
    }
}

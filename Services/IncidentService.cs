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

    public async Task<IReadOnlyCollection<SupportIncidentResponse>> GetSupportIncidentsAsync(
        IncidentDbContext dbContext,
        string? status,
        string? level,
        CancellationToken cancellationToken = default)
    {
        var phoneLookup = await BuildReporterPhoneLookupAsync(dbContext, cancellationToken);
        var incidents = await dbContext.Incidents
            .AsNoTracking()
            .ApplyFilters(
                new IncidentQueryParameters(null, status, level, null, null, null, null, "created_desc"),
                analysisService.NormalizeLevel,
                analysisService.NormalizeStatus)
            .ApplySort("created_desc")
            .ToListAsync(cancellationToken);

        return incidents
            .Select(item => item.ToSupportResponse(phoneLookup))
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

        var phoneLookup = await BuildReporterPhoneLookupAsync(dbContext, cancellationToken);
        return incident.ToSupportResponse(phoneLookup);
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

        assessment = AnalyzeAssessment(
            request.Title,
            request.Detail,
            string.IsNullOrWhiteSpace(request.Level) ? request.Category : request.Level);
        var now = DateTimeOffset.UtcNow;
        var category = ResolveCategory(request, assessment);

        incident = new IncidentRecord
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Detail = request.Detail?.Trim() ?? string.Empty,
            Category = category,
            Level = assessment.Level,
            UrgencyScore = assessment.UrgencyScore,
            ClassificationReason = assessment.Reason,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            District = GeoLocationUtils.ResolveDistrict(request.Latitude, request.Longitude),
            TimeLabel = DateTimeOffset.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            Status = assessment.UrgencyScore >= 85
                ? IncidentStatuses.DangXacMinh
                : IncidentStatuses.MoiTiepNhan,
            Source = "user",
            ReporterName = actor.DisplayName,
            ReporterPhone = actor.Username,
            LastUpdatedBy = actor.DisplayName,
            InternalNote = assessment.Recommendation,
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

    private static async Task<IReadOnlyDictionary<string, string>> BuildReporterPhoneLookupAsync(
        IncidentDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var accountPhones = await dbContext.Accounts
            .AsNoTracking()
            .Select(item => new
            {
                item.DisplayName,
                Phone = item.Username
            })
            .ToListAsync(cancellationToken);

        return accountPhones
            .GroupBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Phone).FirstOrDefault() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);
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

        // Start a transaction to ensure atomicity
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var payload = incident.ToResponse();
            
            // Delete related audit logs for this incident
            var relatedAuditLogs = await dbContext.AuditLogs
                .Where(log => log.EntityId == incidentId.ToString())
                .ToListAsync(cancellationToken);
            
            if (relatedAuditLogs.Count > 0)
            {
                dbContext.AuditLogs.RemoveRange(relatedAuditLogs);
            }
            
            // Delete the incident
            dbContext.Incidents.Remove(incident);
            
            // Save all changes to database
            await dbContext.SaveChangesAsync(cancellationToken);
            
            // Commit the transaction
            await transaction.CommitAsync(cancellationToken);
            
            // Send SignalR notification after successful database operation
            await hubContext.Clients.All.SendAsync("IncidentDeleted", new { id = incidentId }, cancellationToken);

            return payload;
        }
        catch
        {
            // Rollback the transaction on error
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync();
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

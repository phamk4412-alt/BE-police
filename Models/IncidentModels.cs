using System.Text.Json.Serialization;

namespace PoliceBackend.Models;

public sealed class IncidentRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Category { get; set; } = "Chua xac dinh";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string District { get; set; } = string.Empty;
    public string TimeLabel { get; set; } = string.Empty;
    public string Status { get; set; } = "Moi tiep nhan";
    public string Source { get; set; } = "user";
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterPhone { get; set; } = string.Empty;
    public string LastUpdatedBy { get; set; } = string.Empty;
    public string ImageUrls { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CreateIncidentRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? Category { get; set; }
    public string? CustomCategory { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<IFormFile> Images { get; set; } = [];
}

public sealed record UpdateIncidentStatusRequest(string Status);

public sealed record AnalyzeIncidentRequest(string? Title, string? Detail);

public sealed record IncidentQueryParameters(
    string? Search,
    string? Status,
    string? Source,
    string? District,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Sort);

public sealed record IncidentResponse(
    Guid Id,
    string Title,
    string Detail,
    string Category,
    double Latitude,
    double Longitude,
    string District,
    string TimeLabel,
    string Status,
    string Source,
    string ReporterName,
    string ReporterPhone,
    string LastUpdatedBy,
    IReadOnlyCollection<string> ImageUrls,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SupportIncidentResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reporterName")] string ReporterName,
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("imageUrls")] IReadOnlyCollection<string> ImageUrls,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

public sealed record SupportIncidentStatusUpdateRequest(
    [property: JsonPropertyName("status")] string Status);

public sealed record IncidentAnalysisResponse(
    string Category,
    bool ShouldCallEmergency);

public sealed record IncidentAssessment(
    string Category,
    bool ShouldCallEmergency);

public sealed record IncidentProfile(
    string Label,
    string Category,
    int BaseScore,
    params string[] Keywords);

public sealed record CreateIncidentResult(
    string Message,
    IncidentAnalysisResponse Analysis,
    IncidentResponse Incident);

public sealed record UpdateIncidentStatusResult(
    string Message,
    IncidentResponse Incident);

public sealed record LocationResolutionRequest(string Location);

public sealed record LocationResolutionResponse(
    double Latitude,
    double Longitude,
    string District,
    bool IsWithinCoverage);

public sealed record NearbyAlertResponse(
    Guid Id,
    string Title,
    string Category,
    string Status,
    string District,
    double Latitude,
    double Longitude,
    double DistanceKm,
    DateTimeOffset CreatedAt);

public sealed record HotspotResponse(
    string District,
    int OpenIncidentCount,
    string RecommendedAction);

public sealed record PatrolVehicleResponse(
    string UnitCode,
    string District,
    string Status,
    int AssignedIncidentCount,
    int PriorityScore,
    string RecommendedFocus);

public sealed record DispatchQueueItemResponse(
    Guid IncidentId,
    string Title,
    string Status,
    string District,
    string ReporterName,
    DateTimeOffset CreatedAt,
    string RecommendedAction);

public sealed record SupportCenterOverviewResponse(
    int PendingCalls,
    int ActiveDispatches,
    int ActiveIncidentCount,
    IReadOnlyCollection<HotspotResponse> Hotspots);

public sealed record MetricCountResponse(string Key, int Count);

public sealed record AdminStatisticsResponse(
    int TotalIncidents,
    int OpenIncidents,
    int ResolvedIncidents,
    IReadOnlyCollection<MetricCountResponse> ByStatus,
    IReadOnlyCollection<MetricCountResponse> ByDistrict,
    int AuditLogCount);

public sealed record PoliceLocationRequest(
    double Latitude,
    double Longitude,
    string? ShiftId,
    string? Status,
    string? Username,
    string? DisplayName);

public sealed record PoliceLocationResponse(
    string Username,
    string DisplayName,
    string Role,
    double Latitude,
    double Longitude,
    string District,
    string? ShiftId,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record EndPoliceShiftRequest(string? Username);

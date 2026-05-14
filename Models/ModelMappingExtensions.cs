namespace PoliceBackend.Models;

public static class ModelMappingExtensions
{
    public static NewsResponse ToResponse(this NewsRecord news) => new(
        news.Id,
        news.Title,
        news.Summary,
        news.Content,
        news.ThumbnailUrl,
        news.Category,
        news.IsFeatured,
        news.FeaturedOrder,
        news.Status,
        news.PublishedAt,
        news.CreatedAt,
        news.UpdatedAt,
        news.CreatedBy,
        news.UpdatedBy);

    public static IncidentResponse ToResponse(this IncidentRecord incident) => new(
        incident.Id,
        incident.Title,
        incident.Detail,
        incident.Category,
        incident.Level,
        incident.UrgencyScore,
        incident.ClassificationReason,
        incident.Latitude,
        incident.Longitude,
        incident.District,
        incident.TimeLabel,
        incident.Status,
        incident.Source,
        incident.ReporterName,
        incident.ReporterPhone,
        incident.LastUpdatedBy,
        incident.InternalNote,
        incident.ImageUrls
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        incident.CreatedAt,
        incident.UpdatedAt);

    public static SupportIncidentResponse ToSupportResponse(
        this IncidentRecord incident,
        IReadOnlyDictionary<string, string> accountPhones) => new(
        incident.Id,
        incident.Title,
        incident.Detail,
        incident.Category,
        incident.Latitude,
        incident.Longitude,
        incident.Level,
        ToSupportStatus(incident.Status),
        incident.ReporterName,
        ResolvePhone(incident, accountPhones),
        incident.ImageUrls
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        incident.CreatedAt);

    public static IncidentAnalysisResponse ToResponse(this IncidentAssessment assessment) => new(
        assessment.Category,
        assessment.Level,
        assessment.UrgencyScore,
        assessment.Reason,
        assessment.ShouldCallEmergency,
        assessment.Recommendation);

    public static AuditLogResponse ToResponse(this AuditLogRecord auditLog) => new(
        auditLog.Id,
        auditLog.Action,
        auditLog.EntityType,
        auditLog.EntityId,
        auditLog.ActorUsername,
        auditLog.ActorDisplayName,
        auditLog.ActorRole,
        auditLog.Summary,
        auditLog.Detail,
        auditLog.IpAddress,
        auditLog.CreatedAt);

    private static string ToSupportStatus(string status) => status switch
    {
        "Moi tiep nhan" => "new",
        "Da xu ly" => "done",
        _ => "processing"
    };

    private static string ResolvePhone(
        IncidentRecord incident,
        IReadOnlyDictionary<string, string> accountPhones)
    {
        if (!string.IsNullOrWhiteSpace(incident.ReporterPhone))
        {
            return incident.ReporterPhone;
        }

        return accountPhones.TryGetValue(incident.ReporterName, out var phone)
            ? phone
            : string.Empty;
    }
}

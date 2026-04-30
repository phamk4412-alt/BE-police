namespace PoliceBackend.Models;

public static class ModelMappingExtensions
{
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
        incident.LastUpdatedBy,
        incident.InternalNote,
        incident.CreatedAt,
        incident.UpdatedAt);

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
}

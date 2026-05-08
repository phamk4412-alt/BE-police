namespace PoliceBackend.Models;

public sealed class AuditLogRecord
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ActorUsername { get; set; } = string.Empty;
    public string ActorDisplayName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record AuditLogResponse(
    Guid Id,
    string Action,
    string EntityType,
    string EntityId,
    string ActorUsername,
    string ActorDisplayName,
    string ActorRole,
    string Summary,
    string Detail,
    string IpAddress,
    DateTimeOffset CreatedAt);

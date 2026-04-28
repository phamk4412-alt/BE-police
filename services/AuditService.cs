using Microsoft.EntityFrameworkCore;
using PoliceBackend.Database;
using PoliceBackend.Models;

namespace PoliceBackend.Services;

public sealed class AuditService(AuthService authService)
{
    public async Task WriteAsync(
        IncidentDbContext dbContext,
        HttpContext context,
        string action,
        string entityType,
        string entityId,
        string summary,
        string detail,
        ActorSnapshot? actor = null,
        bool saveChanges = true,
        CancellationToken cancellationToken = default)
    {
        var resolvedActor = actor ?? authService.GetActorSnapshot(context.User);

        dbContext.AuditLogs.Add(new AuditLogRecord
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUsername = resolvedActor.Username,
            ActorDisplayName = resolvedActor.DisplayName,
            ActorRole = resolvedActor.Role,
            Summary = summary,
            Detail = detail,
            IpAddress = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (saveChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyCollection<AuditLogResponse>> GetLogsAsync(
        IncidentDbContext dbContext,
        string? action,
        string? actorRole,
        string? entityType,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim().ToLowerInvariant();
            query = query.Where(item => item.Action.ToLower() == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(actorRole))
        {
            var normalizedActorRole = actorRole.Trim().ToLowerInvariant();
            query = query.Where(item => item.ActorRole.ToLower() == normalizedActorRole);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var normalizedEntityType = entityType.Trim().ToLowerInvariant();
            query = query.Where(item => item.EntityType.ToLower() == normalizedEntityType);
        }

        var take = Math.Clamp(limit ?? 50, 1, 200);

        var logs = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return logs
            .Select(item => item.ToResponse())
            .ToArray();
    }
}

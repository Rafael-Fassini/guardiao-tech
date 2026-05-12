using Guardiao.Domain.Enums;
using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class AuditLog
{
    private AuditLog()
    {
    }

    public AuditLog(AuditActorType actorType, string action, string entityName, string entityId, string details)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new InvariantViolationException("Audit action is required.");
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new InvariantViolationException("Audit entity name is required.");
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            throw new InvariantViolationException("Audit entity id is required.");
        }

        Id = Guid.NewGuid();
        ActorType = actorType;
        Action = action.Trim();
        EntityName = entityName.Trim();
        EntityId = entityId.Trim();
        Details = details?.Trim() ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public AuditActorType ActorType { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}

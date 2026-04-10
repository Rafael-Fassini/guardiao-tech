namespace Guardiao.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Action { get; private set; }
    public string Entity { get; private set; }
    public Guid EntityId { get; private set; }
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public string? User { get; private set; }
    public string? Details { get; private set; }

    public AuditLog(string action, string entity, Guid entityId, string? user, string? details)
    {
        Action = action;
        Entity = entity;
        EntityId = entityId;
        User = user;
        Details = details;
    }
}

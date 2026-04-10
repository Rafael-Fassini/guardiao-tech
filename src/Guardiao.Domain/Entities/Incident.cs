namespace Guardiao.Domain.Entities;

public class Incident
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ProtectiveCaseId { get; private set; }
    public Guid DetectionEventId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string Status { get; private set; }

    public Incident(Guid protectiveCaseId, Guid detectionEventId, string status)
    {
        ProtectiveCaseId = protectiveCaseId;
        DetectionEventId = detectionEventId;
        Status = status;
    }
}

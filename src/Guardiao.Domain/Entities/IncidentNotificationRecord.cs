using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class IncidentNotificationRecord
{
    private IncidentNotificationRecord()
    {
    }

    public IncidentNotificationRecord(
        Guid incidentId,
        string eventType,
        string channel,
        string deliveryStatus,
        int attemptCount,
        bool hasEvidence,
        string details,
        DateTime? completedAtUtc = null)
    {
        if (incidentId == Guid.Empty)
        {
            throw new InvariantViolationException("Notification record must reference an incident.");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvariantViolationException("Notification event type is required.");
        }

        if (string.IsNullOrWhiteSpace(channel))
        {
            throw new InvariantViolationException("Notification channel is required.");
        }

        if (string.IsNullOrWhiteSpace(deliveryStatus))
        {
            throw new InvariantViolationException("Notification delivery status is required.");
        }

        if (attemptCount <= 0)
        {
            throw new InvariantViolationException("Notification attempt count must be greater than zero.");
        }

        Id = Guid.NewGuid();
        IncidentId = incidentId;
        EventType = eventType.Trim();
        Channel = channel.Trim();
        DeliveryStatus = deliveryStatus.Trim();
        AttemptCount = attemptCount;
        HasEvidence = hasEvidence;
        Details = details?.Trim() ?? string.Empty;
        CreatedAtUtc = DateTime.UtcNow;
        CompletedAtUtc = completedAtUtc ?? CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid IncidentId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string DeliveryStatus { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }
    public bool HasEvidence { get; private set; }
    public string Details { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
}

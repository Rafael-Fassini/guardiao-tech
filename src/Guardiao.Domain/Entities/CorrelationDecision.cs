using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class CorrelationDecision
{
    private CorrelationDecision()
    {
    }

    public CorrelationDecision(
        Guid protectedCaseId,
        Guid candidateEventId,
        bool createsIncident,
        CorrelationReasonCode reasonCode)
    {
        Id = Guid.NewGuid();
        ProtectedCaseId = protectedCaseId;
        CandidateEventId = candidateEventId;
        CreatesIncident = createsIncident;
        ReasonCode = reasonCode;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProtectedCaseId { get; private set; }
    public Guid CandidateEventId { get; private set; }
    public bool CreatesIncident { get; private set; }
    public CorrelationReasonCode ReasonCode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

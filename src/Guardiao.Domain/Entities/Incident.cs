using Guardiao.Domain.Enums;
using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.Entities;

public class Incident
{
    private Incident()
    {
    }

    public Incident(Guid protectedCaseId, Guid candidateEventId)
    {
        if (protectedCaseId == Guid.Empty)
        {
            throw new InvariantViolationException("Incident must reference a protected case.");
        }

        if (candidateEventId == Guid.Empty)
        {
            throw new InvariantViolationException("Incident must reference a candidate event.");
        }

        Id = Guid.NewGuid();
        ProtectedCaseId = protectedCaseId;
        CandidateEventId = candidateEventId;
        Status = IncidentStatus.PendingReview;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProtectedCaseId { get; private set; }
    public Guid CandidateEventId { get; private set; }
    public IncidentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ReviewedAtUtc { get; private set; }
    public DateTime? EscalatedAtUtc { get; private set; }
    public string? ReviewNotes { get; private set; }

    public void ConfirmReview(string reviewNotes)
    {
        if (Status != IncidentStatus.PendingReview)
        {
            throw new ForbiddenStateTransitionException("Only incidents pending review can be confirmed.");
        }

        Status = IncidentStatus.Confirmed;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = reviewNotes?.Trim();
    }

    public void Dismiss(string reviewNotes)
    {
        if (Status != IncidentStatus.PendingReview)
        {
            throw new ForbiddenStateTransitionException("Only incidents pending review can be dismissed.");
        }

        Status = IncidentStatus.Dismissed;
        ReviewedAtUtc = DateTime.UtcNow;
        ReviewNotes = reviewNotes?.Trim();
    }

    public void Escalate()
    {
        if (Status != IncidentStatus.Confirmed)
        {
            throw new ForbiddenStateTransitionException("Incident escalation beyond review cannot occur without human validation.");
        }

        Status = IncidentStatus.Escalated;
        EscalatedAtUtc = DateTime.UtcNow;
    }

    public void MarkPendingReviewEscalated()
    {
        if (Status != IncidentStatus.PendingReview)
        {
            throw new ForbiddenStateTransitionException("Only incidents pending review can be escalated by SLA.");
        }

        if (EscalatedAtUtc is not null)
        {
            throw new ForbiddenStateTransitionException("Incident was already escalated.");
        }

        EscalatedAtUtc = DateTime.UtcNow;
    }
}

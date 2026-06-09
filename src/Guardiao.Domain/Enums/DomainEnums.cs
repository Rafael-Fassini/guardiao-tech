namespace Guardiao.Domain.Enums;

public enum IncidentStatus
{
    PendingReview = 1,
    Confirmed = 2,
    Dismissed = 3,
    Escalated = 4
}

public enum EvidenceArtifactType
{
    FaceCrop = 1,
    Snapshot = 2,
    AuditAttachment = 3
}

public enum AuditActorType
{
    System = 1,
    Operator = 2,
    Integration = 3
}

public enum MonitoredSubjectRole
{
    ProtectedWoman = 1,
    Aggressor = 2
}

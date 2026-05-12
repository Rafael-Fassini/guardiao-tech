using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class ProtectedCase
{
    private ProtectedCase()
    {
    }

    public ProtectedCase(
        ExternalCaseId externalCaseId,
        long version,
        Guid institutionId,
        Guid personProjectionId,
        MonitoringStatus monitoringStatus,
        ConsentStatus consentStatus)
    {
        if (version <= 0)
        {
            throw new InvariantViolationException("Protected case version must be greater than zero.");
        }

        if (institutionId == Guid.Empty)
        {
            throw new InvariantViolationException("Protected case must belong to an institution.");
        }

        if (personProjectionId == Guid.Empty)
        {
            throw new InvariantViolationException("Protected case requires a person projection.");
        }

        Id = Guid.NewGuid();
        ExternalCaseId = externalCaseId;
        Version = version;
        InstitutionId = institutionId;
        PersonProjectionId = personProjectionId;
        MonitoringStatus = monitoringStatus;
        ConsentStatus = consentStatus;
        CreatedAt = DateTime.UtcNow;
        LastSynchronizedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public ExternalCaseId ExternalCaseId { get; private set; }
    public long Version { get; private set; }
    public Guid InstitutionId { get; private set; }
    public Guid PersonProjectionId { get; private set; }
    public MonitoringStatus MonitoringStatus { get; private set; }
    public ConsentStatus ConsentStatus { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime LastSynchronizedAt { get; private set; }
    public string LastSyncStatus { get; private set; } = "pending";
    public string? LastSyncFailureReason { get; private set; }

    public void BindPersonProjection(Guid personProjectionId)
    {
        if (personProjectionId == Guid.Empty)
        {
            throw new InvariantViolationException("Protected case requires a valid person projection id.");
        }

        PersonProjectionId = personProjectionId;
    }

    public void SynchronizeFromSource(long version, MonitoringStatus monitoringStatus, ConsentStatus consentStatus, DateTime synchronizedAtUtc)
    {
        if (version < Version)
        {
            throw new ForbiddenStateTransitionException("Stale source version cannot overwrite the current protected case.");
        }

        Version = version;
        MonitoringStatus = monitoringStatus;
        ConsentStatus = consentStatus;
        LastSynchronizedAt = synchronizedAtUtc;
        LastSyncStatus = "succeeded";
        LastSyncFailureReason = null;
    }

    public void MarkSyncFailure(string failureReason, DateTime synchronizedAtUtc)
    {
        LastSynchronizedAt = synchronizedAtUtc;
        LastSyncStatus = "failed";
        LastSyncFailureReason = string.IsNullOrWhiteSpace(failureReason) ? "unknown" : failureReason.Trim();
    }
}

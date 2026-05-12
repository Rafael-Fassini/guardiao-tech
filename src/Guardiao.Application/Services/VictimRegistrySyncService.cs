using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Application.Services;

public class VictimRegistrySyncService
{
    private readonly IVictimRegistryPort _victimRegistryPort;
    private readonly ICaseProjectionRepository _caseProjectionRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IClock _clock;

    public VictimRegistrySyncService(
        IVictimRegistryPort victimRegistryPort,
        ICaseProjectionRepository caseProjectionRepository,
        IAuditLogRepository auditLogRepository,
        IClock clock)
    {
        _victimRegistryPort = victimRegistryPort;
        _caseProjectionRepository = caseProjectionRepository;
        _auditLogRepository = auditLogRepository;
        _clock = clock;
    }

    public async Task<SyncCaseResult> SyncCaseAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _victimRegistryPort.GetCaseAsync(externalCaseId, cancellationToken);
            if (snapshot is null)
            {
                await _caseProjectionRepository.MarkSyncFailureAsync(externalCaseId, "Case not found in victim registry.", _clock.UtcNow, cancellationToken);
                return SyncCaseResult.NotFound(externalCaseId.Value);
            }

            return await SyncSnapshotAsync(snapshot, cancellationToken);
        }
        catch (Exception ex)
        {
            await _caseProjectionRepository.MarkSyncFailureAsync(externalCaseId, ex.Message, _clock.UtcNow, cancellationToken);
            throw;
        }
    }

    public async Task<SyncCaseResult> SyncSnapshotAsync(VictimRegistryCaseSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await _caseProjectionRepository.GetByExternalIdAsync(snapshot.ExternalCaseId, cancellationToken);
        if (existing is not null && snapshot.Version < existing.Version)
        {
            return SyncCaseResult.Stale(snapshot.ExternalCaseId.Value, snapshot.Version, existing.Version);
        }

        var protectedCase = new ProtectedCase(
            snapshot.ExternalCaseId,
            snapshot.Version,
            existing?.InstitutionId ?? Guid.NewGuid(),
            existing?.PersonProjectionId == Guid.Empty ? Guid.NewGuid() : existing?.PersonProjectionId ?? Guid.NewGuid(),
            snapshot.MonitoringStatus,
            snapshot.ConsentStatus);

        if (existing is not null)
        {
            protectedCase = existing;
            protectedCase.SynchronizeFromSource(snapshot.Version, snapshot.MonitoringStatus, snapshot.ConsentStatus, _clock.UtcNow);
        }

        var personProjection = new PersonProjection(
            snapshot.ExternalPersonId,
            protectedCase.Id,
            snapshot.FullName,
            snapshot.IsBystander,
            snapshot.UpdatedAtUtc);

        protectedCase.BindPersonProjection(personProjection.Id);
        if (existing is null)
        {
            protectedCase.SynchronizeFromSource(snapshot.Version, snapshot.MonitoringStatus, snapshot.ConsentStatus, _clock.UtcNow);
        }

        await _caseProjectionRepository.UpsertAsync(protectedCase, personProjection, cancellationToken);
        await _auditLogRepository.AddAsync(
            new AuditLog(
                AuditActorType.Integration,
                "victim_registry.sync",
                nameof(ProtectedCase),
                protectedCase.Id.ToString(),
                $"external_case_id={snapshot.ExternalCaseId};version={snapshot.Version}"),
            cancellationToken);

        return existing is null
            ? SyncCaseResult.Created(snapshot.ExternalCaseId.Value, snapshot.Version)
            : SyncCaseResult.Updated(snapshot.ExternalCaseId.Value, snapshot.Version);
    }
}

public sealed record SyncCaseResult(string ExternalCaseId, string Outcome, long? RemoteVersion = null, long? LocalVersion = null)
{
    public static SyncCaseResult Created(string externalCaseId, long remoteVersion) => new(externalCaseId, "created", remoteVersion);
    public static SyncCaseResult Updated(string externalCaseId, long remoteVersion) => new(externalCaseId, "updated", remoteVersion);
    public static SyncCaseResult Stale(string externalCaseId, long remoteVersion, long localVersion) => new(externalCaseId, "stale_ignored", remoteVersion, localVersion);
    public static SyncCaseResult NotFound(string externalCaseId) => new(externalCaseId, "not_found");
}

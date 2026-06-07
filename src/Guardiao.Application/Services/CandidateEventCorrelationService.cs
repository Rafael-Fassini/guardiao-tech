using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Application.Services;

public sealed class CandidateEventCorrelationService
{
    private readonly ICaseProjectionRepository _caseProjectionRepository;
    private readonly IMonitoringRuleRepository _monitoringRuleRepository;
    private readonly ICandidateEventRepository _candidateEventRepository;
    private readonly ICorrelationDecisionRepository _correlationDecisionRepository;
    private readonly IIncidentRepository _incidentRepository;
    private readonly INotificationPort _notificationPort;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IShortLivedStatePort _shortLivedStatePort;
    private readonly IClock _clock;
    private readonly CorrelationEngineOptions _options;

    public CandidateEventCorrelationService(
        ICaseProjectionRepository caseProjectionRepository,
        IMonitoringRuleRepository monitoringRuleRepository,
        ICandidateEventRepository candidateEventRepository,
        ICorrelationDecisionRepository correlationDecisionRepository,
        IIncidentRepository incidentRepository,
        INotificationPort notificationPort,
        IAuditLogRepository auditLogRepository,
        IShortLivedStatePort shortLivedStatePort,
        IClock clock,
        CorrelationEngineOptions options)
    {
        _caseProjectionRepository = caseProjectionRepository;
        _monitoringRuleRepository = monitoringRuleRepository;
        _candidateEventRepository = candidateEventRepository;
        _correlationDecisionRepository = correlationDecisionRepository;
        _incidentRepository = incidentRepository;
        _notificationPort = notificationPort;
        _auditLogRepository = auditLogRepository;
        _shortLivedStatePort = shortLivedStatePort;
        _clock = clock;
        _options = options;
    }

    public async Task<CorrelationResult> ConsumeAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default)
    {
        var existingCandidateEvent = await _candidateEventRepository.GetByIdAsync(candidateEvent.Id, cancellationToken);
        if (existingCandidateEvent is not null)
        {
            var existingDecisions = await _correlationDecisionRepository.ListByCandidateEventAsync(candidateEvent.Id, cancellationToken);
            if (existingDecisions.Count > 0)
            {
                var existingDecision = existingDecisions
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .First();
                var existingIncident = existingDecision.CreatesIncident
                    ? await _incidentRepository.GetByCandidateEventIdAsync(candidateEvent.Id, cancellationToken)
                    : null;
                return new CorrelationResult(existingDecision, existingIncident, true);
            }

            candidateEvent = existingCandidateEvent;
        }
        else
        {
            await _candidateEventRepository.AddAsync(candidateEvent, cancellationToken);
        }

        var protectedCase = await _caseProjectionRepository.GetByIdAsync(candidateEvent.ProtectedCaseId, cancellationToken);
        if (protectedCase is null)
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "CASE_NOT_FOUND", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        if (!protectedCase.MonitoringStatus.IsEnabled)
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "MONITORING_DISABLED", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        var rules = await _monitoringRuleRepository.ListByCaseAsync(protectedCase.Id, cancellationToken);
        var currentTime = TimeOnly.FromDateTime(candidateEvent.OccurredAtUtc);
        var matchingRule = rules.FirstOrDefault(rule => rule.AppliesTo(candidateEvent.CameraScope, currentTime));

        if (matchingRule is null)
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "OUT_OF_SCOPE", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        var duplicateKey = $"candidate:{candidateEvent.ProtectedCaseId}:{candidateEvent.CameraScope.SiteId}:{candidateEvent.CameraScope.CameraId}:{candidateEvent.MatchScore.Value:0.000}";
        var existing = await _shortLivedStatePort.GetAsync(duplicateKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "DUPLICATE_SUPPRESSED", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        await _shortLivedStatePort.SetAsync(
            duplicateKey,
            candidateEvent.Id.ToString(),
            _options.DuplicateSuppressionWindow,
            cancellationToken);

        var activeIncident = await _incidentRepository.FindLatestActiveByCaseAsync(protectedCase.Id, cancellationToken);
        if (activeIncident is not null &&
            _clock.UtcNow - activeIncident.CreatedAtUtc <= _options.CooldownWindow)
        {
            var cooldownDecision = await PersistDecisionAsync(candidateEvent, false, "COOLDOWN_ACTIVE", cancellationToken);
            return new CorrelationResult(cooldownDecision, activeIncident);
        }

        var incident = new Incident(protectedCase.Id, candidateEvent.Id);
        await _incidentRepository.AddAsync(incident, cancellationToken);
        await _notificationPort.NotifyIncidentCreatedAsync(incident, cancellationToken);
        await _auditLogRepository.AddAsync(
            new AuditLog(
                AuditActorType.System,
                "incident.created",
                nameof(Incident),
                incident.Id.ToString(),
                $"candidate_event_id={candidateEvent.Id};case_id={protectedCase.Id}"),
            cancellationToken);

        var createDecision = await PersistDecisionAsync(candidateEvent, true, "RULE_MATCH", cancellationToken);
        return new CorrelationResult(createDecision, incident);
    }

    private async Task<CorrelationDecision> PersistDecisionAsync(BiometricCandidateEvent candidateEvent, bool createsIncident, string reasonCode, CancellationToken cancellationToken)
    {
        var decision = new CorrelationDecision(
            candidateEvent.ProtectedCaseId,
            candidateEvent.Id,
            createsIncident,
            new CorrelationReasonCode(reasonCode));

        await _correlationDecisionRepository.AddAsync(decision, cancellationToken);
        return decision;
    }
}

public sealed record CorrelationResult(CorrelationDecision Decision, Incident? Incident, bool WasDuplicate = false);

using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using System.Collections.Concurrent;

namespace Guardiao.Application.Services;

public sealed class CandidateEventCorrelationService
{
    private readonly ICaseProjectionRepository _caseProjectionRepository;
    private readonly IMonitoringRuleRepository _monitoringRuleRepository;
    private readonly ICandidateEventRepository _candidateEventRepository;
    private readonly ICorrelationDecisionRepository _correlationDecisionRepository;
    private readonly IIncidentRepository _incidentRepository;
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

        var counterpartMatch = await FindCounterpartMatchAsync(protectedCase, candidateEvent, cancellationToken);
        if (counterpartMatch is null)
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "CO_PRESENCE_NOT_FOUND", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        var protectedWomanCaseId = ResolveProtectedWomanCaseId(protectedCase, counterpartMatch.ProtectedCase);
        var aggressorCaseId = ResolveAggressorCaseId(protectedCase, counterpartMatch.ProtectedCase);
        var activeIncidentCutoffUtc = _clock.UtcNow - _options.CoPresenceWindow;
        var activeIncident = await _incidentRepository.FindLatestActiveByCaseAndCameraScopeAsync(
            protectedWomanCaseId,
            candidateEvent.CameraScope,
            activeIncidentCutoffUtc,
            cancellationToken);
        if (activeIncident is not null)
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "ENCOUNTER_ALREADY_OPEN", cancellationToken);
            return new CorrelationResult(decision, activeIncident);
        }

        var encounterKey = BuildEncounterKey(protectedWomanCaseId, aggressorCaseId, candidateEvent.CameraScope);
        var existingEncounter = await _shortLivedStatePort.GetAsync(encounterKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingEncounter))
        {
            var decision = await PersistDecisionAsync(candidateEvent, false, "ENCOUNTER_SUPPRESSED", cancellationToken);
            return new CorrelationResult(decision, null);
        }

        await _shortLivedStatePort.SetAsync(
            encounterKey,
            candidateEvent.Id.ToString(),
            _options.CoPresenceWindow,
            cancellationToken);

        var incident = new Incident(protectedWomanCaseId, candidateEvent.Id);
        await _incidentRepository.AddAsync(incident, cancellationToken);
        await _auditLogRepository.AddAsync(
            new AuditLog(
                AuditActorType.System,
                "incident.created",
                nameof(Incident),
                incident.Id.ToString(),
                $"candidate_event_id={candidateEvent.Id};protected_woman_case_id={protectedWomanCaseId};aggressor_case_id={aggressorCaseId};camera_id={candidateEvent.CameraScope.CameraId}"),
            cancellationToken);

        var createDecision = await PersistDecisionAsync(candidateEvent, true, "CO_PRESENCE_MATCH", cancellationToken);
        return new CorrelationResult(createDecision, incident);
    }

    private async Task<CounterpartMatch?> FindCounterpartMatchAsync(
        ProtectedCase currentCase,
        BiometricCandidateEvent candidateEvent,
        CancellationToken cancellationToken)
    {
        var counterpartRole = ResolveCounterpartRole(currentCase.SubjectRole);
        var occurredFromUtc = candidateEvent.OccurredAtUtc - _options.CoPresenceWindow;
        var recentEvents = await _candidateEventRepository.ListRecentByCameraScopeAsync(
            candidateEvent.CameraScope,
            occurredFromUtc,
            candidateEvent.OccurredAtUtc,
            cancellationToken);

        var caseCache = new ConcurrentDictionary<Guid, ProtectedCase?>();
        var ruleCache = new ConcurrentDictionary<Guid, IReadOnlyCollection<MonitoringRule>>();

        foreach (var recentEvent in recentEvents
                     .Where(x => x.Id != candidateEvent.Id && x.ProtectedCaseId != candidateEvent.ProtectedCaseId)
                     .OrderByDescending(x => x.MatchScore.Value)
                     .ThenByDescending(x => x.OccurredAtUtc))
        {
            var recentCase = await GetCaseAsync(recentEvent.ProtectedCaseId, caseCache, cancellationToken);
            if (recentCase is null ||
                recentCase.SubjectRole != counterpartRole ||
                !recentCase.MonitoringStatus.IsEnabled)
            {
                continue;
            }

            if (_options.RequireSameSiteForCoPresence &&
                recentEvent.CameraScope.SiteId != candidateEvent.CameraScope.SiteId)
            {
                continue;
            }

            var rules = await GetRulesAsync(recentCase.Id, ruleCache, cancellationToken);
            var recentEventTime = TimeOnly.FromDateTime(recentEvent.OccurredAtUtc);
            var ruleMatches = rules.Any(rule => rule.AppliesTo(recentEvent.CameraScope, recentEventTime));
            if (!ruleMatches)
            {
                continue;
            }

            return new CounterpartMatch(recentCase, recentEvent);
        }

        return null;
    }

    private async Task<ProtectedCase?> GetCaseAsync(
        Guid protectedCaseId,
        ConcurrentDictionary<Guid, ProtectedCase?> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(protectedCaseId, out var cached))
        {
            return cached;
        }

        var value = await _caseProjectionRepository.GetByIdAsync(protectedCaseId, cancellationToken);
        cache[protectedCaseId] = value;
        return value;
    }

    private async Task<IReadOnlyCollection<MonitoringRule>> GetRulesAsync(
        Guid protectedCaseId,
        ConcurrentDictionary<Guid, IReadOnlyCollection<MonitoringRule>> cache,
        CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(protectedCaseId, out var cached))
        {
            return cached;
        }

        var value = await _monitoringRuleRepository.ListByCaseAsync(protectedCaseId, cancellationToken);
        cache[protectedCaseId] = value;
        return value;
    }

    private static MonitoredSubjectRole ResolveCounterpartRole(MonitoredSubjectRole subjectRole)
    {
        return subjectRole switch
        {
            MonitoredSubjectRole.ProtectedWoman => MonitoredSubjectRole.Aggressor,
            MonitoredSubjectRole.Aggressor => MonitoredSubjectRole.ProtectedWoman,
            _ => throw new InvalidOperationException($"Unsupported subject role '{subjectRole}'.")
        };
    }

    private static Guid ResolveProtectedWomanCaseId(ProtectedCase currentCase, ProtectedCase counterpartCase)
    {
        return currentCase.SubjectRole == MonitoredSubjectRole.ProtectedWoman
            ? currentCase.Id
            : counterpartCase.Id;
    }

    private static Guid ResolveAggressorCaseId(ProtectedCase currentCase, ProtectedCase counterpartCase)
    {
        return currentCase.SubjectRole == MonitoredSubjectRole.Aggressor
            ? currentCase.Id
            : counterpartCase.Id;
    }

    private static string BuildEncounterKey(Guid protectedWomanCaseId, Guid aggressorCaseId, CameraScope cameraScope)
    {
        return $"encounter:{protectedWomanCaseId}:{aggressorCaseId}:{cameraScope.SiteId}:{cameraScope.CameraId}";
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
internal sealed record CounterpartMatch(ProtectedCase ProtectedCase, BiometricCandidateEvent CandidateEvent);

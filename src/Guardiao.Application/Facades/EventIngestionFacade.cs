using Guardiao.Application.DTOs;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Application.Facades;

public class EventIngestionFacade
{
    public (BiometricCandidateEvent CandidateEvent, CorrelationDecision Decision, Incident? Incident) IngestCandidateEvent(
        BiometricCandidateEventDto dto,
        ProtectedCase protectedCase,
        MonitoringRule monitoringRule)
    {
        var candidateEvent = new BiometricCandidateEvent(
            dto.ProtectedCaseId,
            new CameraScope(dto.SiteId, dto.CameraId),
            new MatchScore(dto.MatchScore),
            dto.OccurredAtUtc);

        var withinRuleWindow = monitoringRule.AppliesTo(
            candidateEvent.CameraScope,
            TimeOnly.FromDateTime(candidateEvent.OccurredAtUtc));

        var shouldCreateIncident = protectedCase.MonitoringStatus.IsEnabled && withinRuleWindow;
        var decision = new CorrelationDecision(
            protectedCase.Id,
            candidateEvent.Id,
            shouldCreateIncident,
            new CorrelationReasonCode(shouldCreateIncident ? "RULE_MATCH" : "OUT_OF_SCOPE"));

        Incident? incident = null;
        if (shouldCreateIncident)
        {
            incident = new Incident(protectedCase.Id, candidateEvent.Id);
        }

        return (candidateEvent, decision, incident);
    }
}

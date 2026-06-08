using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/candidate-events")]
[Authorize(Policy = AuthorizationPolicies.CandidateEventsIngest)]
public class CandidateEventsController : ControllerBase
{
    private readonly CandidateEventCorrelationService _correlationService;
    private readonly GuardiaoDbContext _dbContext;
    private readonly IEvidenceStoragePort _storage;
    private readonly INotificationPort _notificationPort;
    private readonly IMetricsPort _metrics;
    private readonly ILogger<CandidateEventsController> _logger;

    public CandidateEventsController(
        CandidateEventCorrelationService correlationService,
        GuardiaoDbContext dbContext,
        IEvidenceStoragePort storage,
        INotificationPort notificationPort,
        IMetricsPort metrics,
        ILogger<CandidateEventsController> logger)
    {
        _correlationService = correlationService;
        _dbContext = dbContext;
        _storage = storage;
        _notificationPort = notificationPort;
        _metrics = metrics;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> Post([FromBody] CandidateEventIngestionRequest request, CancellationToken cancellationToken)
    {
        var candidateEvent = new BiometricCandidateEvent(
            request.EventId,
            request.ProtectedCaseId,
            new CameraScope(request.SiteId, request.CameraId),
            new MatchScore(request.MatchScore),
            request.OccurredAtUtc);

        var startedAt = DateTime.UtcNow;
        var result = await _correlationService.ConsumeAsync(candidateEvent, cancellationToken);
        _metrics.IncrementCounter("candidate_events_ingested_total");
        _metrics.RecordLatency("candidate_event_ingest_latency_ms", DateTime.UtcNow - startedAt);

        if (result.WasDuplicate)
        {
            _metrics.IncrementCounter("candidate_events_duplicates_total");
            _logger.LogInformation(
                "Duplicate candidate event ignored. CandidateEventId={CandidateEventId} Reason={ReasonCode}",
                candidateEvent.Id,
                result.Decision.ReasonCode.Value);
        }
        else
        {
            _logger.LogInformation(
                "Candidate event ingested. CandidateEventId={CandidateEventId} CreatesIncident={CreatesIncident} Reason={ReasonCode}",
                candidateEvent.Id,
                result.Decision.CreatesIncident,
                result.Decision.ReasonCode.Value);
        }

        if (result.Incident is not null && result.Decision.CreatesIncident)
        {
            _metrics.IncrementCounter("candidate_events_incidents_created_total");
        }

        var persistedEvidenceCount = 0;
        if (!result.WasDuplicate && result.Incident is not null && request.Evidences.Count > 0)
        {
            persistedEvidenceCount = await PersistEvidenceArtifactsAsync(candidateEvent, result.Incident, request.Evidences, cancellationToken);
        }

        if (!result.WasDuplicate && result.Incident is not null && result.Decision.CreatesIncident)
        {
            await _notificationPort.NotifyIncidentCreatedAsync(new IncidentNotification(
                result.Incident.Id,
                result.Incident.ProtectedCaseId,
                result.Incident.CandidateEventId,
                result.Incident.CreatedAtUtc,
                result.Incident.Status.ToString(),
                persistedEvidenceCount > 0,
                result.Incident.EscalatedAtUtc), cancellationToken);
        }

        return Ok(new CandidateEventIngestionResponse(
            candidateEvent.Id,
            result.WasDuplicate,
            result.Decision.CreatesIncident,
            result.Decision.ReasonCode.Value,
            result.Incident?.Id));
    }

    private async Task<int> PersistEvidenceArtifactsAsync(
        BiometricCandidateEvent candidateEvent,
        Incident incident,
        IReadOnlyCollection<CandidateEventEvidenceRequest> evidences,
        CancellationToken cancellationToken)
    {
        var persistedCount = 0;

        foreach (var evidence in evidences)
        {
            try
            {
                if (evidence.Content.Length == 0)
                {
                    throw new ArgumentException("Evidence payload content was empty.", nameof(evidences));
                }

                var artifactType = ParseArtifactType(evidence.ArtifactType);
                await using var content = new MemoryStream(evidence.Content, writable: false);
                var storagePath = await _storage.StoreAsync(content, evidence.FileName, evidence.ContentType, cancellationToken);

                var artifact = new EvidenceArtifact(
                    incident.Id,
                    candidateEvent.Id,
                    artifactType,
                    storagePath,
                    evidence.ContentType,
                    RetentionMode.CaseBound);

                _dbContext.EvidenceArtifacts.Add(artifact);
                _dbContext.AuditLogs.Add(new AuditLog(
                    AuditActorType.System,
                    "evidence_artifact.created",
                    nameof(EvidenceArtifact),
                    artifact.Id.ToString(),
                    $"incident_id={incident.Id};candidate_event_id={candidateEvent.Id};artifact_type={artifactType}"));

                _metrics.IncrementCounter("evidences_created_total", ("artifact", artifactType.ToString()));
                _metrics.AddCounter("evidence_bytes_uploaded_total", evidence.Content.LongLength, ("artifact", artifactType.ToString()));
                persistedCount++;
                _logger.LogInformation(
                    "Evidence artifact persisted. IncidentId={IncidentId} CandidateEventId={CandidateEventId} ArtifactType={ArtifactType}",
                    incident.Id,
                    candidateEvent.Id,
                    artifactType);
            }
            catch (Exception ex)
            {
                _metrics.IncrementCounter("evidence_upload_failures_total", ("artifact", evidence.ArtifactType));
                _logger.LogWarning(
                    ex,
                    "Evidence persistence failed. IncidentId={IncidentId} CandidateEventId={CandidateEventId} ArtifactType={ArtifactType}",
                    incident.Id,
                    candidateEvent.Id,
                    evidence.ArtifactType);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return persistedCount;
    }

    private static EvidenceArtifactType ParseArtifactType(string artifactType)
    {
        return Enum.TryParse<EvidenceArtifactType>(artifactType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException($"Unsupported evidence artifact type '{artifactType}'.", nameof(artifactType));
    }
}

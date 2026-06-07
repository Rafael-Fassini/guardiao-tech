using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
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
    private readonly IMetricsPort _metrics;
    private readonly ILogger<CandidateEventsController> _logger;

    public CandidateEventsController(
        CandidateEventCorrelationService correlationService,
        IMetricsPort metrics,
        ILogger<CandidateEventsController> logger)
    {
        _correlationService = correlationService;
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

        return Ok(new CandidateEventIngestionResponse(
            candidateEvent.Id,
            result.WasDuplicate,
            result.Decision.CreatesIncident,
            result.Decision.ReasonCode.Value,
            result.Incident?.Id));
    }
}

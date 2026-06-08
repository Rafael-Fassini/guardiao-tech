using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize(Policy = AuthorizationPolicies.IncidentsRead)]
public class IncidentsController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public IncidentsController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Set<Incident>()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new IncidentListItemResponse(
                x.Id,
                x.ProtectedCaseId,
                x.CandidateEventId,
                x.Status.ToString(),
                x.CreatedAtUtc,
                x.ReviewedAtUtc,
                x.EscalatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Set<Incident>()
            .Where(x => x.Id == id)
            .Select(x => new IncidentDetailResponse(
                x.Id,
                x.ProtectedCaseId,
                x.CandidateEventId,
                x.Status.ToString(),
                x.CreatedAtUtc,
                x.ReviewedAtUtc,
                x.EscalatedAtUtc,
                x.ReviewNotes))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id:guid}/review/confirm")]
    [Authorize(Policy = AuthorizationPolicies.IncidentsReview)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] IncidentReviewRequest request, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.Set<Incident>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var evidenceCount = await _dbContext.EvidenceArtifacts.CountAsync(x => x.IncidentId == incident.Id, cancellationToken);
        incident.ConfirmReview(request.ReviewNotes);
        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            "incident.review.confirmed",
            nameof(Incident),
            incident.Id.ToString(),
            $"status={IncidentStatus.Confirmed};evidence_count={evidenceCount}"));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new IncidentDetailResponse(
            incident.Id,
            incident.ProtectedCaseId,
            incident.CandidateEventId,
            incident.Status.ToString(),
            incident.CreatedAtUtc,
            incident.ReviewedAtUtc,
            incident.EscalatedAtUtc,
            incident.ReviewNotes));
    }

    [HttpPost("{id:guid}/review/dismiss")]
    [Authorize(Policy = AuthorizationPolicies.IncidentsReview)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> Dismiss(Guid id, [FromBody] IncidentReviewRequest request, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.Set<Incident>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (incident is null)
        {
            return NotFound();
        }

        var evidenceCount = await _dbContext.EvidenceArtifacts.CountAsync(x => x.IncidentId == incident.Id, cancellationToken);
        incident.Dismiss(request.ReviewNotes);
        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            "incident.review.dismissed",
            nameof(Incident),
            incident.Id.ToString(),
            $"status={IncidentStatus.Dismissed};evidence_count={evidenceCount}"));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new IncidentDetailResponse(
            incident.Id,
            incident.ProtectedCaseId,
            incident.CandidateEventId,
            incident.Status.ToString(),
            incident.CreatedAtUtc,
            incident.ReviewedAtUtc,
            incident.EscalatedAtUtc,
            incident.ReviewNotes));
    }
}

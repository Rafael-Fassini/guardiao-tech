using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/incidents/{incidentId:guid}/evidences")]
[Authorize(Policy = AuthorizationPolicies.IncidentsRead)]
public sealed class IncidentEvidencesController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;
    private readonly IEvidenceStoragePort _storage;

    public IncidentEvidencesController(GuardiaoDbContext dbContext, IEvidenceStoragePort storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid incidentId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Incidents.AnyAsync(x => x.Id == incidentId, cancellationToken))
        {
            return NotFound();
        }

        var items = await _dbContext.EvidenceArtifacts
            .Where(x => x.IncidentId == incidentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new IncidentEvidenceResponse(
                x.Id,
                x.IncidentId,
                x.CandidateEventId,
                x.ArtifactType.ToString(),
                x.ContentType,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{evidenceId:guid}")]
    public async Task<IActionResult> Get(Guid incidentId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var item = await _dbContext.EvidenceArtifacts
            .Where(x => x.IncidentId == incidentId && x.Id == evidenceId)
            .Select(x => new IncidentEvidenceResponse(
                x.Id,
                x.IncidentId,
                x.CandidateEventId,
                x.ArtifactType.ToString(),
                x.ContentType,
                x.CreatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{evidenceId:guid}/content")]
    public async Task<IActionResult> Download(Guid incidentId, Guid evidenceId, CancellationToken cancellationToken)
    {
        var artifact = await _dbContext.EvidenceArtifacts
            .FirstOrDefaultAsync(x => x.IncidentId == incidentId && x.Id == evidenceId, cancellationToken);
        if (artifact is null)
        {
            return NotFound();
        }

        var stream = await _storage.OpenReadAsync(artifact.StoragePath, cancellationToken);
        var fileName = $"{artifact.ArtifactType.ToString().ToLowerInvariant()}-{artifact.Id:N}{ResolveExtension(artifact.ContentType)}";
        return File(stream, artifact.ContentType, fileName);
    }

    private static string ResolveExtension(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => string.Empty
        };
    }
}

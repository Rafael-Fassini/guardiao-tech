using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class OperationsController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public OperationsController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var incidentCount = await _dbContext.Incidents.CountAsync(cancellationToken);
        var caseCount = await _dbContext.ProtectedCases.CountAsync(cancellationToken);
        var cameraCount = await _dbContext.Cameras.CountAsync(cancellationToken);
        var auditEntryCount = await _dbContext.AuditLogs.CountAsync(cancellationToken);

        var recentIncidents = await _dbContext.Incidents
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new RecentIncidentResponse(
                x.Id,
                x.ProtectedCaseId,
                x.Status.ToString(),
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var recentAuditEntries = await _dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new AuditEntryResponse(
                x.Id,
                x.ActorType.ToString(),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(new OperationsSummaryResponse(
            incidentCount,
            caseCount,
            cameraCount,
            auditEntryCount,
            recentIncidents,
            recentAuditEntries));
    }
}

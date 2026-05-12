using Guardiao.Api.Infrastructure;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = AuthorizationPolicies.AuditRead)]
public class AuditController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public AuditController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var entries = await _dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                ActorType = x.ActorType.ToString(),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }
}

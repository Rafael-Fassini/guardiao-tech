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
[Route("api/cameras")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class CamerasController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public CamerasController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Set<Camera>()
            .OrderBy(x => x.Name)
            .Select(x => new CameraResponse(
                x.Id,
                x.SiteId,
                x.Name,
                x.StreamEndpoint,
                x.IsEnabled))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("{id:guid}/state")]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> PutState(Guid id, [FromBody] UpdateCameraStateRequest request, CancellationToken cancellationToken)
    {
        var camera = await _dbContext.Cameras.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (camera is null)
        {
            return NotFound();
        }

        if (request.IsEnabled)
        {
            camera.Enable();
        }
        else
        {
            camera.Disable();
        }

        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            "camera.state.updated",
            nameof(Camera),
            camera.Id.ToString(),
            $"enabled={request.IsEnabled}"));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CameraResponse(
            camera.Id,
            camera.SiteId,
            camera.Name,
            camera.StreamEndpoint,
            camera.IsEnabled));
    }
}

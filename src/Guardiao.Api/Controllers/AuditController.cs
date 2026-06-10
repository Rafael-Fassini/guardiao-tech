using Guardiao.Api.Infrastructure;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
            .Select(x => new Guardiao.Api.Contracts.AuditEntryResponse(
                x.Id,
                x.ActorType.ToString(),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }

    [HttpPost("session")]
    [Authorize(Policy = AuthorizationPolicies.MetadataRead)]
    public async Task<IActionResult> WriteSessionEntry([FromBody] AuditSessionRequest request, CancellationToken cancellationToken)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("session.login" or "session.logout"))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.Action)] = ["Invalid audit session action. Use session.login or session.logout."]
            })
            {
                Title = "Validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }

        var userName = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userName))
        {
            return Unauthorized();
        }

        var role = User.FindFirstValue(ClaimTypes.Role) ?? "viewer";
        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            action,
            "OperationsSession",
            userName,
            $"role={role};source=web-panel"));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Accepted();
    }
}

public sealed class AuditSessionRequest
{
    public string Action { get; set; } = string.Empty;
}

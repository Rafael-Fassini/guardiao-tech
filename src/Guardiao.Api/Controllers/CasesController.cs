using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/cases")]
[Authorize(Policy = AuthorizationPolicies.CasesRead)]
public class CasesController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public CasesController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var cases = await _dbContext.ProtectedCases
            .OrderBy(x => x.CreatedAt)
            .Select(x => new ProtectedCaseListItemResponse(
                x.Id,
                x.ExternalCaseId.Value,
                x.SubjectRole.ToString(),
                x.Version,
                x.MonitoringStatus.Value,
                x.ConsentStatus.Value,
                x.LastSynchronizedAt,
                x.LastSyncStatus))
            .ToListAsync(cancellationToken);

        return Ok(cases);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _dbContext.ProtectedCases
            .Where(x => x.Id == id)
            .Select(x => new ProtectedCaseDetailResponse(
                x.Id,
                x.ExternalCaseId.Value,
                x.SubjectRole.ToString(),
                x.Version,
                x.MonitoringStatus.Value,
                x.ConsentStatus.Value,
                x.PersonProjectionId,
                x.CreatedAt,
                x.LastSynchronizedAt,
                x.LastSyncStatus,
                x.LastSyncFailureReason))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{id:guid}/rules")]
    public async Task<IActionResult> GetRules(Guid id, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ProtectedCases.AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var rules = await _dbContext.Set<MonitoringRule>()
            .Where(x => x.ProtectedCaseId == id)
            .Select(x => new MonitoringRuleResponse(
                x.Id,
                x.CameraScope.SiteId,
                x.CameraScope.CameraId,
                x.ActiveWindow.StartsAt,
                x.ActiveWindow.EndsAt,
                x.IsEnabled))
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    [HttpPut("{id:guid}/subject-role")]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> PutSubjectRole(Guid id, [FromBody] UpdateProtectedCaseSubjectRoleRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<MonitoredSubjectRole>(request.SubjectRole, ignoreCase: true, out var subjectRole))
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(request.SubjectRole)] = ["Invalid subject role. Use ProtectedWoman or Aggressor."]
            })
            {
                Title = "Validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            });
        }

        var protectedCase = await _dbContext.ProtectedCases
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (protectedCase is null)
        {
            return NotFound();
        }

        protectedCase.Reclassify(subjectRole);

        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            "protected_case.subject_role.updated",
            nameof(ProtectedCase),
            protectedCase.Id.ToString(),
            $"case_id={protectedCase.Id};subject_role={subjectRole}"));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ProtectedCaseDetailResponse(
            protectedCase.Id,
            protectedCase.ExternalCaseId.Value,
            protectedCase.SubjectRole.ToString(),
            protectedCase.Version,
            protectedCase.MonitoringStatus.Value,
            protectedCase.ConsentStatus.Value,
            protectedCase.PersonProjectionId,
            protectedCase.CreatedAt,
            protectedCase.LastSynchronizedAt,
            protectedCase.LastSyncStatus,
            protectedCase.LastSyncFailureReason));
    }

    [HttpPut("{id:guid}/rules/{ruleId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> PutRule(Guid id, Guid ruleId, [FromBody] UpdateMonitoringRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await _dbContext.Set<MonitoringRule>()
            .FirstOrDefaultAsync(x => x.Id == ruleId && x.ProtectedCaseId == id, cancellationToken);

        if (rule is null)
        {
            return NotFound();
        }

        rule.Reconfigure(
            new CameraScope(request.SiteId, request.CameraId),
            new TimeWindow(request.StartsAt, request.EndsAt),
            request.IsEnabled);

        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Operator,
            "monitoring_rule.updated",
            nameof(MonitoringRule),
            rule.Id.ToString(),
            $"case_id={id};enabled={request.IsEnabled}"));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new MonitoringRuleResponse(
            rule.Id,
            rule.CameraScope.SiteId,
            rule.CameraScope.CameraId,
            rule.ActiveWindow.StartsAt,
            rule.ActiveWindow.EndsAt,
            rule.IsEnabled));
    }
}

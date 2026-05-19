using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Web.Services;

public sealed class OperationsPanelService
{
    private readonly GuardiaoDbContext _dbContext;

    public OperationsPanelService(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<Incident>> ListIncidentsAsync(CancellationToken cancellationToken = default)
        => _dbContext.Incidents.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task<Incident?> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Incidents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<ProtectedCase>> ListCasesAsync(CancellationToken cancellationToken = default)
        => _dbContext.ProtectedCases.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task<ProtectedCase?> GetCaseAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.ProtectedCases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<MonitoringRule>> ListRulesAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
        => _dbContext.MonitoringRules.Where(x => x.ProtectedCaseId == protectedCaseId).ToListAsync(cancellationToken);

    public Task<List<Site>> ListSitesAsync(CancellationToken cancellationToken = default)
        => _dbContext.Sites.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<Camera>> ListCamerasAsync(CancellationToken cancellationToken = default)
        => _dbContext.Cameras.OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<AuditLog>> ListAuditAsync(CancellationToken cancellationToken = default)
        => _dbContext.AuditLogs.OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(cancellationToken);

    public async Task ConfirmIncidentAsync(Guid incidentId, string reviewNotes, CancellationToken cancellationToken = default)
    {
        var incident = await _dbContext.Incidents.FirstAsync(x => x.Id == incidentId, cancellationToken);
        incident.ConfirmReview(reviewNotes);
        _dbContext.AuditLogs.Add(new AuditLog(
            Guardiao.Domain.Enums.AuditActorType.Operator,
            "web.incident.confirmed",
            nameof(Incident),
            incident.Id.ToString(),
            reviewNotes));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DismissIncidentAsync(Guid incidentId, string reviewNotes, CancellationToken cancellationToken = default)
    {
        var incident = await _dbContext.Incidents.FirstAsync(x => x.Id == incidentId, cancellationToken);
        incident.Dismiss(reviewNotes);
        _dbContext.AuditLogs.Add(new AuditLog(
            Guardiao.Domain.Enums.AuditActorType.Operator,
            "web.incident.dismissed",
            nameof(Incident),
            incident.Id.ToString(),
            reviewNotes));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRuleAsync(Guid ruleId, Guid siteId, Guid cameraId, TimeOnly startsAt, TimeOnly endsAt, bool enabled, CancellationToken cancellationToken = default)
    {
        var rule = await _dbContext.MonitoringRules.FirstAsync(x => x.Id == ruleId, cancellationToken);
        rule.Reconfigure(new Guardiao.Domain.ValueObjects.CameraScope(siteId, cameraId), new Guardiao.Domain.ValueObjects.TimeWindow(startsAt, endsAt), enabled);
        _dbContext.AuditLogs.Add(new AuditLog(
            Guardiao.Domain.Enums.AuditActorType.Operator,
            "web.rule.updated",
            nameof(MonitoringRule),
            rule.Id.ToString(),
            $"enabled={enabled}"));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ToggleCameraAsync(Guid cameraId, bool enabled, CancellationToken cancellationToken = default)
    {
        var camera = await _dbContext.Cameras.FirstAsync(x => x.Id == cameraId, cancellationToken);
        if (enabled)
        {
            camera.Enable();
        }
        else
        {
            camera.Disable();
        }

        _dbContext.AuditLogs.Add(new AuditLog(
            Guardiao.Domain.Enums.AuditActorType.Operator,
            "web.camera.toggled",
            nameof(Camera),
            camera.Id.ToString(),
            $"enabled={enabled}"));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

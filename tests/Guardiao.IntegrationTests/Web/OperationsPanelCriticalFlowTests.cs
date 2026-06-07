using Guardiao.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Web;

public class OperationsPanelCriticalFlowTests
{
    [Fact]
    public async Task ConfirmIncident_ShouldCallApiAndUpdateBackendState()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        var incidentId = Guid.NewGuid();
        factory.ApiHandler.Incidents.Add(new IncidentState
        {
            Id = incidentId,
            ProtectedCaseId = Guid.NewGuid(),
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow
        });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OperationsPanelService>();

        await service.ConfirmIncidentAsync(incidentId, "validado pela operacao");

        var incident = factory.ApiHandler.Incidents.Single(x => x.Id == incidentId);
        var auditEntry = factory.ApiHandler.AuditEntries.Single(x => x.EntityId == incidentId.ToString());

        Assert.Equal("Confirmed", incident.Status);
        Assert.Equal("validado pela operacao", incident.ReviewNotes);
        Assert.Equal("incident.review.confirmed", auditEntry.Action);
    }

    [Fact]
    public async Task UpdateRule_ShouldCallApiAndWriteAudit()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        var caseId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        factory.ApiHandler.Rules.Add(new MonitoringRuleState
        {
            Id = ruleId,
            ProtectedCaseId = caseId,
            SiteId = Guid.NewGuid(),
            CameraId = Guid.NewGuid(),
            StartsAt = new TimeOnly(8, 0),
            EndsAt = new TimeOnly(18, 0),
            IsEnabled = true
        });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OperationsPanelService>();

        await service.UpdateRuleAsync(caseId, ruleId, Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), new TimeOnly(17, 0), false);

        var rule = factory.ApiHandler.Rules.Single(x => x.Id == ruleId);
        var auditEntry = factory.ApiHandler.AuditEntries.Single(x => x.EntityId == ruleId.ToString());

        Assert.False(rule.IsEnabled);
        Assert.Equal(new TimeOnly(9, 0), rule.StartsAt);
        Assert.Equal(new TimeOnly(17, 0), rule.EndsAt);
        Assert.Equal("monitoring_rule.updated", auditEntry.Action);
    }

    [Fact]
    public async Task ToggleCamera_ShouldCallApiAndWriteAudit()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        var cameraId = Guid.NewGuid();
        factory.ApiHandler.Cameras.Add(new CameraState
        {
            Id = cameraId,
            SiteId = Guid.NewGuid(),
            Name = "Camera 01",
            StreamEndpoint = "rtsp://camera-01",
            IsEnabled = true
        });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OperationsPanelService>();

        await service.ToggleCameraAsync(cameraId, false);

        var camera = factory.ApiHandler.Cameras.Single(x => x.Id == cameraId);
        var auditEntry = factory.ApiHandler.AuditEntries.Single(x => x.EntityId == cameraId.ToString());

        Assert.False(camera.IsEnabled);
        Assert.Equal("camera.state.updated", auditEntry.Action);
    }
}

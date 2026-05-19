using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Web;

public class OperationsPanelCriticalFlowTests
{
    [Fact]
    public async Task ConfirmIncident_ShouldPersistReviewAndAuditTrail()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        var incidentId = Guid.Empty;

        await factory.SeedAsync(db =>
        {
            var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
            db.Incidents.Add(incident);
            incidentId = incident.Id;
        });

        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<OperationsPanelService>();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        await service.ConfirmIncidentAsync(incidentId, "validado pela operacao");

        var incident = await db.Incidents.SingleAsync(x => x.Id == incidentId);
        var auditEntry = await db.AuditLogs.SingleAsync(x => x.EntityId == incidentId.ToString());

        Assert.Equal(Guardiao.Domain.Enums.IncidentStatus.Confirmed, incident.Status);
        Assert.Equal("validado pela operacao", incident.ReviewNotes);
        Assert.Equal("web.incident.confirmed", auditEntry.Action);
    }
}

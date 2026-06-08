using System.Net;
using System.Net.Http.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Notifications;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class IncidentNotificationsIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public IncidentNotificationsIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EscalatePendingAsync_ShouldMarkIncidentAndExposeNotificationHistory()
    {
        _factory.RegistryHandler.ResetWebhook();

        Guid incidentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
            db.Incidents.Add(incident);
            db.EvidenceArtifacts.Add(new EvidenceArtifact(
                incident.Id,
                incident.CandidateEventId,
                EvidenceArtifactType.Snapshot,
                "evidence/snapshot.jpg",
                "image/jpeg",
                RetentionMode.CaseBound));
            await db.SaveChangesAsync();

            incidentId = incident.Id;
            db.Entry(incident).Property(x => x.CreatedAtUtc).CurrentValue = DateTime.UtcNow.AddMinutes(-30);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<PendingIncidentEscalationService>();
            var escalatedCount = await service.EscalatePendingAsync();
            Assert.Equal(1, escalatedCount);
        }

        using (var verifyScope = _factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var incident = await verifyDb.Incidents.SingleAsync(x => x.Id == incidentId);
            Assert.NotNull(incident.EscalatedAtUtc);
            Assert.Equal(IncidentStatus.PendingReview, incident.Status);
            Assert.Contains(await verifyDb.AuditLogs.Where(x => x.EntityId == incidentId.ToString()).ToListAsync(), x => x.Action == "incident.escalated");
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "operator-escalation");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await client.GetAsync($"/api/incidents/{incidentId}/notifications");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notifications = await response.Content.ReadFromJsonAsync<List<IncidentNotificationHistoryItem>>();
        var item = Assert.Single(notifications!);
        Assert.Equal("incident.escalated", item.EventType);
        Assert.Equal("Sent", item.DeliveryStatus);
        Assert.True(item.HasEvidence);
        Assert.Single(_factory.RegistryHandler.WebhookPayloads);
    }

    [Fact]
    public async Task ReviewAfterEscalation_ShouldWriteDedicatedAudit()
    {
        Guid incidentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
            db.Incidents.Add(incident);
            await db.SaveChangesAsync();
            incident.MarkPendingReviewEscalated();
            await db.SaveChangesAsync();
            incidentId = incident.Id;
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "operator-review");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await client.PostAsJsonAsync($"/api/incidents/{incidentId}/review/confirm", new
        {
            reviewNotes = "reviewed after escalation"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var auditEntries = await verifyDb.AuditLogs.Where(x => x.EntityId == incidentId.ToString()).ToListAsync();
        Assert.Contains(auditEntries, x => x.Action == "incident.review.after_escalation");
    }

    private sealed record IncidentNotificationHistoryItem(
        Guid Id,
        Guid IncidentId,
        string EventType,
        string Channel,
        string DeliveryStatus,
        int AttemptCount,
        bool HasEvidence,
        string Details,
        DateTime CreatedAtUtc,
        DateTime? CompletedAtUtc);
}

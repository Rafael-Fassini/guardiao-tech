using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class CandidateEventsControllerIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public CandidateEventsControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCandidateEvent_ShouldPersistAndCreateIncident_WhenCorrelationMatches()
    {
        _factory.RegistryHandler.ResetWebhook();
        var caseId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            db.ProtectedCases.Add(new ProtectedCase(
                new ExternalCaseId("case-ingest"),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted));
            await db.SaveChangesAsync();

            var protectedCase = await db.ProtectedCases.OrderByDescending(x => x.CreatedAt).FirstAsync();
            caseId = protectedCase.Id;
            db.MonitoringRules.Add(new MonitoringRule(
                caseId,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true));
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        client.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = caseId,
            siteId,
            cameraId,
            matchScore = 0.91,
            occurredAtUtc = DateTime.UtcNow,
            evidences = new[]
            {
                new
                {
                    artifactType = "Snapshot",
                    fileName = "snapshot.jpg",
                    contentType = "image/jpeg",
                    content = new byte[] { 1, 2, 3, 4 }
                },
                new
                {
                    artifactType = "FaceCrop",
                    fileName = "face-crop.jpg",
                    contentType = "image/jpeg",
                    content = new byte[] { 5, 6, 7, 8 }
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(payload.GetProperty("wasDuplicate").GetBoolean());
        Assert.True(payload.GetProperty("createsIncident").GetBoolean());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.True(await verifyDb.BiometricCandidateEvents.AnyAsync(x => x.Id == eventId));
        Assert.True(await verifyDb.Incidents.AnyAsync(x => x.CandidateEventId == eventId));
        Assert.True(await verifyDb.CorrelationDecisions.AnyAsync(x => x.CandidateEventId == eventId));
        Assert.Equal(2, await verifyDb.EvidenceArtifacts.CountAsync(x => x.CandidateEventId == eventId));
        var incidentId = await verifyDb.Incidents
            .Where(x => x.CandidateEventId == eventId)
            .Select(x => x.Id)
            .SingleAsync();
        var notification = await verifyDb.IncidentNotificationRecords.SingleAsync(x => x.IncidentId == incidentId);
        Assert.Equal("incident.created", notification.EventType);
        Assert.Equal("Sent", notification.DeliveryStatus);
        Assert.True(notification.HasEvidence);
        Assert.Single(_factory.RegistryHandler.WebhookPayloads);
        Assert.Contains("\"hasEvidence\":true", _factory.RegistryHandler.WebhookPayloads.Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCandidateEvent_ShouldBeIdempotent_ForDuplicateEventId()
    {
        _factory.RegistryHandler.ResetWebhook();
        var caseId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new ProtectedCase(
                new ExternalCaseId("case-idempotent"),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted);
            db.ProtectedCases.Add(protectedCase);
            db.MonitoringRules.Add(new MonitoringRule(
                protectedCase.Id,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true));
            await db.SaveChangesAsync();
            caseId = protectedCase.Id;
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        client.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var eventId = Guid.NewGuid();
        var request = new
        {
            eventId,
            protectedCaseId = caseId,
            siteId,
            cameraId,
            matchScore = 0.95,
            occurredAtUtc = DateTime.UtcNow
        };

        var first = await client.PostAsJsonAsync("/api/candidate-events", request);
        var second = await client.PostAsJsonAsync("/api/candidate-events", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondPayload = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondPayload.GetProperty("wasDuplicate").GetBoolean());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.Equal(1, await verifyDb.BiometricCandidateEvents.CountAsync(x => x.Id == eventId));
        Assert.Equal(1, await verifyDb.Incidents.CountAsync(x => x.CandidateEventId == eventId));
        Assert.Equal(1, await verifyDb.CorrelationDecisions.CountAsync(x => x.CandidateEventId == eventId));
        Assert.Single(_factory.RegistryHandler.WebhookPayloads);
    }

    [Fact]
    public async Task PostCandidateEvent_ShouldRetryNotification_WhenWebhookFailsTransiently()
    {
        _factory.RegistryHandler.ResetWebhook();
        _factory.RegistryHandler.EnqueueWebhookResponse(HttpStatusCode.InternalServerError);
        _factory.RegistryHandler.EnqueueWebhookResponse(HttpStatusCode.OK);

        var caseId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new ProtectedCase(
                new ExternalCaseId("case-retry"),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted);
            db.ProtectedCases.Add(protectedCase);
            db.MonitoringRules.Add(new MonitoringRule(
                protectedCase.Id,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true));
            await db.SaveChangesAsync();
            caseId = protectedCase.Id;
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        client.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = caseId,
            siteId,
            cameraId,
            matchScore = 0.97,
            occurredAtUtc = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var incidentId = await verifyDb.Incidents
            .Where(x => x.CandidateEventId == eventId)
            .Select(x => x.Id)
            .SingleAsync();
        var record = await verifyDb.IncidentNotificationRecords
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync(x => x.IncidentId == incidentId && x.EventType == "incident.created");

        Assert.Equal("Sent", record.DeliveryStatus);
        Assert.Equal(2, record.AttemptCount);
        Assert.Equal(2, _factory.RegistryHandler.WebhookPayloads.Count);
    }

    [Fact]
    public async Task PostCandidateEvent_ShouldNotBlockIncidentCreation_WhenNotificationFails()
    {
        _factory.RegistryHandler.ResetWebhook();
        _factory.RegistryHandler.EnqueueWebhookResponse(HttpStatusCode.InternalServerError);
        _factory.RegistryHandler.EnqueueWebhookResponse(HttpStatusCode.InternalServerError);
        _factory.RegistryHandler.EnqueueWebhookResponse(HttpStatusCode.InternalServerError);

        var caseId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new ProtectedCase(
                new ExternalCaseId("case-notify-fail"),
                1,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted);
            db.ProtectedCases.Add(protectedCase);
            db.MonitoringRules.Add(new MonitoringRule(
                protectedCase.Id,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true));
            await db.SaveChangesAsync();
            caseId = protectedCase.Id;
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        client.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = caseId,
            siteId,
            cameraId,
            matchScore = 0.88,
            occurredAtUtc = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.True(await verifyDb.Incidents.AnyAsync(x => x.CandidateEventId == eventId));
        var incidentId = await verifyDb.Incidents
            .Where(x => x.CandidateEventId == eventId)
            .Select(x => x.Id)
            .SingleAsync();
        var record = await verifyDb.IncidentNotificationRecords
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstAsync(x => x.IncidentId == incidentId && x.EventType == "incident.created");
        Assert.Equal("Failed", record.DeliveryStatus);
        Assert.Equal(3, record.AttemptCount);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task PostCandidateEvent_ShouldPersistButNotCreateIncident_WhenDetectionIsIsolated()
    {
        _factory.RegistryHandler.ResetWebhook();
        var scope = await SeedScopeAsync("case-isolated-victim", MonitoredSubjectRole.ProtectedWoman);

        using var client = CreateWorkerClient();
        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = scope.ProtectedCaseId,
            siteId = scope.SiteId,
            cameraId = scope.CameraId,
            matchScore = 0.91,
            occurredAtUtc = DateTime.UtcNow
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(payload.GetProperty("wasDuplicate").GetBoolean());
        Assert.False(payload.GetProperty("createsIncident").GetBoolean());
        Assert.Equal("CO_PRESENCE_NOT_FOUND", payload.GetProperty("decisionReasonCode").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.True(await verifyDb.BiometricCandidateEvents.AnyAsync(x => x.Id == eventId));
        Assert.False(await verifyDb.Incidents.AnyAsync(x => x.CandidateEventId == eventId));
        Assert.True(await verifyDb.CorrelationDecisions.AnyAsync(x => x.CandidateEventId == eventId));
        Assert.Empty(_factory.RegistryHandler.WebhookPayloads);
    }

    [Fact]
    public async Task PostCandidateEvent_ShouldPersistAndCreateIncident_WhenCoPresenceMatches()
    {
        _factory.RegistryHandler.ResetWebhook();
        var protectedWoman = await SeedScopeAsync("case-ingest-victim", MonitoredSubjectRole.ProtectedWoman);
        var aggressor = await SeedScopeAsync("case-ingest-aggressor", MonitoredSubjectRole.Aggressor, protectedWoman.SiteId, protectedWoman.CameraId, protectedWoman.InstitutionId);

        using var client = CreateWorkerClient();

        var firstResponse = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId = Guid.NewGuid(),
            protectedCaseId = protectedWoman.ProtectedCaseId,
            siteId = protectedWoman.SiteId,
            cameraId = protectedWoman.CameraId,
            matchScore = 0.91,
            occurredAtUtc = DateTime.UtcNow.AddSeconds(-30)
        });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = aggressor.ProtectedCaseId,
            siteId = aggressor.SiteId,
            cameraId = aggressor.CameraId,
            matchScore = 0.95,
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
        Assert.Equal("CO_PRESENCE_MATCH", payload.GetProperty("decisionReasonCode").GetString());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        Assert.True(await verifyDb.BiometricCandidateEvents.AnyAsync(x => x.Id == eventId));
        var incident = await verifyDb.Incidents.SingleAsync(x => x.CandidateEventId == eventId);
        Assert.Equal(protectedWoman.ProtectedCaseId, incident.ProtectedCaseId);
        Assert.True(await verifyDb.CorrelationDecisions.AnyAsync(x => x.CandidateEventId == eventId));
        Assert.Equal(2, await verifyDb.EvidenceArtifacts.CountAsync(x => x.CandidateEventId == eventId));
        var notification = await verifyDb.IncidentNotificationRecords.SingleAsync(x => x.IncidentId == incident.Id);
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
        var protectedWoman = await SeedScopeAsync("case-idempotent-victim", MonitoredSubjectRole.ProtectedWoman);
        var aggressor = await SeedScopeAsync("case-idempotent-aggressor", MonitoredSubjectRole.Aggressor, protectedWoman.SiteId, protectedWoman.CameraId, protectedWoman.InstitutionId);

        using var client = CreateWorkerClient();
        await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId = Guid.NewGuid(),
            protectedCaseId = protectedWoman.ProtectedCaseId,
            siteId = protectedWoman.SiteId,
            cameraId = protectedWoman.CameraId,
            matchScore = 0.89,
            occurredAtUtc = DateTime.UtcNow.AddSeconds(-20)
        });

        var eventId = Guid.NewGuid();
        var request = new
        {
            eventId,
            protectedCaseId = aggressor.ProtectedCaseId,
            siteId = aggressor.SiteId,
            cameraId = aggressor.CameraId,
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

        var protectedWoman = await SeedScopeAsync("case-retry-victim", MonitoredSubjectRole.ProtectedWoman);
        var aggressor = await SeedScopeAsync("case-retry-aggressor", MonitoredSubjectRole.Aggressor, protectedWoman.SiteId, protectedWoman.CameraId, protectedWoman.InstitutionId);

        using var client = CreateWorkerClient();
        await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId = Guid.NewGuid(),
            protectedCaseId = protectedWoman.ProtectedCaseId,
            siteId = protectedWoman.SiteId,
            cameraId = protectedWoman.CameraId,
            matchScore = 0.90,
            occurredAtUtc = DateTime.UtcNow.AddSeconds(-15)
        });

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = aggressor.ProtectedCaseId,
            siteId = aggressor.SiteId,
            cameraId = aggressor.CameraId,
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

        var protectedWoman = await SeedScopeAsync("case-notify-fail-victim", MonitoredSubjectRole.ProtectedWoman);
        var aggressor = await SeedScopeAsync("case-notify-fail-aggressor", MonitoredSubjectRole.Aggressor, protectedWoman.SiteId, protectedWoman.CameraId, protectedWoman.InstitutionId);

        using var client = CreateWorkerClient();
        await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId = Guid.NewGuid(),
            protectedCaseId = protectedWoman.ProtectedCaseId,
            siteId = protectedWoman.SiteId,
            cameraId = protectedWoman.CameraId,
            matchScore = 0.90,
            occurredAtUtc = DateTime.UtcNow.AddSeconds(-15)
        });

        var eventId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = aggressor.ProtectedCaseId,
            siteId = aggressor.SiteId,
            cameraId = aggressor.CameraId,
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

    private HttpClient CreateWorkerClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        client.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");
        return client;
    }

    private async Task<SeededScope> SeedScopeAsync(
        string externalCaseId,
        MonitoredSubjectRole subjectRole,
        Guid? siteId = null,
        Guid? cameraId = null,
        Guid? institutionId = null)
    {
        var resolvedSiteId = siteId ?? Guid.NewGuid();
        var resolvedCameraId = cameraId ?? Guid.NewGuid();
        var resolvedInstitutionId = institutionId ?? Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var protectedCase = new ProtectedCase(
            new ExternalCaseId(externalCaseId),
            1,
            resolvedInstitutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            subjectRole);
        db.ProtectedCases.Add(protectedCase);
        db.MonitoringRules.Add(new MonitoringRule(
            protectedCase.Id,
            new CameraScope(resolvedSiteId, resolvedCameraId),
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true));
        await db.SaveChangesAsync();

        return new SeededScope(protectedCase.Id, resolvedInstitutionId, resolvedSiteId, resolvedCameraId);
    }

    private sealed record SeededScope(Guid ProtectedCaseId, Guid InstitutionId, Guid SiteId, Guid CameraId);
}

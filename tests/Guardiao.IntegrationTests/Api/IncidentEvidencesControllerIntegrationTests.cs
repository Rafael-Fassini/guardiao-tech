using System.Net;
using System.Net.Http.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class IncidentEvidencesControllerIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public IncidentEvidencesControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetIncidentEvidences_ShouldListMetadata_AndDownloadContent()
    {
        var caseId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new ProtectedCase(
                new ExternalCaseId("case-evidence"),
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

        using var workerClient = _factory.CreateClient();
        workerClient.DefaultRequestHeaders.Add("X-Worker-Id", "edge-worker-01");
        workerClient.DefaultRequestHeaders.Add("X-Worker-Auth", "worker-test-secret");

        var eventId = Guid.NewGuid();
        var ingest = await workerClient.PostAsJsonAsync("/api/candidate-events", new
        {
            eventId,
            protectedCaseId = caseId,
            siteId,
            cameraId,
            matchScore = 0.93,
            occurredAtUtc = DateTime.UtcNow,
            evidences = new[]
            {
                new
                {
                    artifactType = "Snapshot",
                    fileName = "snapshot.jpg",
                    contentType = "image/jpeg",
                    content = new byte[] { 11, 12, 13, 14 }
                }
            }
        });

        Assert.Equal(HttpStatusCode.OK, ingest.StatusCode);

        Guid incidentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            incidentId = await db.Incidents
                .Where(x => x.CandidateEventId == eventId)
                .Select(x => x.Id)
                .SingleAsync();
        }

        using var panelClient = _factory.CreateClient();
        panelClient.DefaultRequestHeaders.Add("X-Debug-User", "operator-evidence");
        panelClient.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var list = await panelClient.GetAsync($"/api/incidents/{incidentId}/evidences");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = await list.Content.ReadFromJsonAsync<List<IncidentEvidenceListItem>>();
        Assert.NotNull(items);
        var evidence = Assert.Single(items!);
        Assert.Equal("Snapshot", evidence.ArtifactType);

        var content = await panelClient.GetAsync($"/api/incidents/{incidentId}/evidences/{evidence.Id}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/jpeg", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal(new byte[] { 11, 12, 13, 14 }, await content.Content.ReadAsByteArrayAsync());
    }

    private sealed record IncidentEvidenceListItem(
        Guid Id,
        Guid IncidentId,
        Guid? CandidateEventId,
        string ArtifactType,
        string ContentType,
        DateTime CreatedAtUtc);
}

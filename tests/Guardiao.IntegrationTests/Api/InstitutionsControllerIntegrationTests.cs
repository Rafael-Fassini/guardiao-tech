using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Text;
using System.Text.Json;
using Guardiao.Api;
using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class InstitutionsControllerIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly HttpClient _client;
    private readonly GuardiaoApiFactory _factory;

    public InstitutionsControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostInstitutions_ShouldCreateInstitution()
    {
        _client.DefaultRequestHeaders.Add("X-Debug-User", "integration-user");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var request = new CreateInstitutionRequest
        {
            Name = "School A",
            Address = "Main Avenue"
        };

        var response = await _client.PostAsJsonAsync("/api/institutions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task PostInstitutions_ShouldReturnUnauthorized_WhenHeadersAreMissing()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/institutions", new CreateInstitutionRequest
        {
            Name = "No Auth",
            Address = "Street"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostInstitutions_ShouldReturnBadRequest_WhenPayloadIsInvalid()
    {
        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "integration-user");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var response = await _client.PostAsJsonAsync("/api/institutions", new CreateInstitutionRequest
        {
            Name = "",
            Address = "Main Avenue"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostIncidentConfirm_ShouldWriteAuditTrail()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        var institution = new Institution("Institution", "Address");
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-audit"),
            1,
            institution.Id,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);
        var incident = new Incident(protectedCase.Id, Guid.NewGuid());

        db.Institutions.Add(institution);
        db.ProtectedCases.Add(protectedCase);
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-1");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await _client.PostAsJsonAsync($"/api/incidents/{incident.Id}/review/confirm", new { reviewNotes = "looks valid" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var auditEntries = await db.AuditLogs.Where(x => x.EntityId == incident.Id.ToString()).ToListAsync();
        Assert.Contains(auditEntries, x => x.Action == "incident.review.confirmed");

        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "auditor");
        var auditResponse = await _client.GetAsync("/api/audit");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
    }

    [Fact]
    public async Task GetOperationsSummary_ShouldReturnCountsAndRecentItems()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        db.EvidenceArtifacts.RemoveRange(db.EvidenceArtifacts);
        db.Incidents.RemoveRange(db.Incidents);
        db.BiometricCandidateEvents.RemoveRange(db.BiometricCandidateEvents);
        db.PersonProjections.RemoveRange(db.PersonProjections);
        db.ProtectedCases.RemoveRange(db.ProtectedCases);
        db.Cameras.RemoveRange(db.Cameras);
        db.Sites.RemoveRange(db.Sites);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var site = new Site(Guid.NewGuid(), "Site Summary", "Address Summary");
        db.Incidents.Add(new Incident(Guid.NewGuid(), Guid.NewGuid()));
        db.ProtectedCases.Add(new ProtectedCase(
            new ExternalCaseId("case-summary"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted));
        db.Sites.Add(site);
        db.Cameras.Add(new Camera(site.Id, "Camera Summary", "rtsp://summary"));
        db.AuditLogs.Add(new AuditLog(
            Guardiao.Domain.Enums.AuditActorType.Operator,
            "summary.loaded",
            "Operations",
            Guid.NewGuid().ToString(),
            "ok"));
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-summary");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await _client.GetAsync("/api/operations/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("incidentCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("caseCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("cameraCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("cameraViews").GetArrayLength());
    }

    [Fact]
    public async Task GetOperationsSummary_ShouldReturnCameraViewsWithDetectionsAndAggressorAlert()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();

        db.EvidenceArtifacts.RemoveRange(db.EvidenceArtifacts);
        db.Incidents.RemoveRange(db.Incidents);
        db.BiometricCandidateEvents.RemoveRange(db.BiometricCandidateEvents);
        db.PersonProjections.RemoveRange(db.PersonProjections);
        db.ProtectedCases.RemoveRange(db.ProtectedCases);
        db.Cameras.RemoveRange(db.Cameras);
        db.Sites.RemoveRange(db.Sites);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync();

        var institutionId = Guid.NewGuid();
        var site = new Site(institutionId, "Site Operations", "Address Operations");
        var camera = new Camera(site.Id, "Camera Patio", "webcam://0");

        var protectedWoman = new ProtectedCase(
            new ExternalCaseId("case-protected"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);
        var protectedWomanProjection = new PersonProjection(
            new ExternalPersonId("person-protected"),
            protectedWoman.Id,
            "Vitima Teste",
            false,
            DateTime.UtcNow);
        protectedWoman.BindPersonProjection(protectedWomanProjection.Id);

        var aggressor = new ProtectedCase(
            new ExternalCaseId("case-aggressor"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            Guardiao.Domain.Enums.MonitoredSubjectRole.Aggressor);
        var aggressorProjection = new PersonProjection(
            new ExternalPersonId("person-aggressor"),
            aggressor.Id,
            "Agressor Teste",
            false,
            DateTime.UtcNow);
        aggressor.BindPersonProjection(aggressorProjection.Id);

        var protectedWomanCandidate = new BiometricCandidateEvent(
            protectedWoman.Id,
            new CameraScope(site.Id, camera.Id),
            new MatchScore(0.94),
            DateTime.UtcNow.AddMinutes(-2));
        var aggressorCandidate = new BiometricCandidateEvent(
            aggressor.Id,
            new CameraScope(site.Id, camera.Id),
            new MatchScore(0.98),
            DateTime.UtcNow.AddMinutes(-1));

        var protectedWomanIncident = new Incident(protectedWoman.Id, protectedWomanCandidate.Id);
        var aggressorIncident = new Incident(aggressor.Id, aggressorCandidate.Id);

        var protectedWomanSnapshot = new EvidenceArtifact(
            protectedWomanIncident.Id,
            protectedWomanCandidate.Id,
            Guardiao.Domain.Enums.EvidenceArtifactType.Snapshot,
            "evidences/protected.jpg",
            "image/jpeg",
            RetentionMode.CaseBound);
        var aggressorSnapshot = new EvidenceArtifact(
            aggressorIncident.Id,
            aggressorCandidate.Id,
            Guardiao.Domain.Enums.EvidenceArtifactType.Snapshot,
            "evidences/aggressor.jpg",
            "image/jpeg",
            RetentionMode.CaseBound);

        db.Sites.Add(site);
        db.Cameras.Add(camera);
        db.ProtectedCases.AddRange(protectedWoman, aggressor);
        db.PersonProjections.AddRange(protectedWomanProjection, aggressorProjection);
        db.BiometricCandidateEvents.AddRange(protectedWomanCandidate, aggressorCandidate);
        db.Incidents.AddRange(protectedWomanIncident, aggressorIncident);
        db.EvidenceArtifacts.AddRange(protectedWomanSnapshot, aggressorSnapshot);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-summary");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await _client.GetAsync("/api/operations/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<OperationsSummaryResponse>();
        var cameraView = Assert.Single(payload!.CameraViews);

        Assert.Equal(camera.Id, cameraView.CameraId);
        Assert.Equal("Camera Patio", cameraView.CameraName);
        Assert.NotNull(cameraView.LatestSnapshot);
        Assert.Single(cameraView.RecentProtectedWomen);
        Assert.Equal("Vitima Teste", cameraView.RecentProtectedWomen.Single().FullName);
        Assert.NotNull(cameraView.ActiveAlert);
        Assert.Equal("Agressor Teste", cameraView.ActiveAlert!.FullName);
        Assert.Contains("Vitima Teste", cameraView.ActiveAlert.NearbyProtectedWomen);
    }

    [Fact]
    public async Task PutCameraState_ShouldToggleCameraAndWriteAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var camera = new Camera(Guid.NewGuid(), "Camera Toggle", "rtsp://camera-toggle");
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-camera");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await _client.PutAsJsonAsync($"/api/cameras/{camera.Id}/state", new { isEnabled = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var savedCamera = await db.Cameras.SingleAsync(x => x.Id == camera.Id);
        var auditEntries = await db.AuditLogs.Where(x => x.EntityId == camera.Id.ToString()).ToListAsync();

        Assert.False(savedCamera.IsEnabled);
        Assert.Contains(auditEntries, x => x.Action == "camera.state.updated");
    }

    [Fact]
    public async Task GetCameraPreview_ShouldReturnLatestWorkerFrame()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var camera = new Camera(Guid.NewGuid(), "Camera Preview", "webcam://0");
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();

        _factory.WorkerPreviewPort.SetPreview(camera.Id, [1, 2, 3, 4], "image/jpeg", DateTime.UtcNow);

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-preview");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        var response = await _client.GetAsync($"/api/operations/cameras/{camera.Id}/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.Contains("X-Captured-At-Utc"));
        Assert.Equal([1, 2, 3, 4], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task GetCameraLiveStream_ShouldProxyMultipartPreview()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        var camera = new Camera(Guid.NewGuid(), "Camera Live", "webcam://0");
        db.Cameras.Add(camera);
        await db.SaveChangesAsync();

        _factory.WorkerPreviewPort.SetPreview(camera.Id, [1, 2, 3, 4], "image/jpeg", DateTime.UtcNow, 7);

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "operator-live");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "operator");

        using var response = await _client.GetAsync(
            $"/api/operations/cameras/{camera.Id}/live",
            HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("multipart/x-mixed-replace", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        var buffer = new byte[512];
        var bytesRead = await stream.ReadAsync(buffer);
        var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);

        Assert.Contains("guardiao-frame", text);
        Assert.Contains("Content-Type: image/jpeg", text);
    }
}

public class GuardiaoApiFactory : WebApplicationFactory<ApiEntryPoint>
{
    public const string WebhookSecret = "integration-secret";
    private static readonly string DetectionModelPath = CreateTempFile(".xml", "<cascade/>");
    private static readonly string EmbeddingModelPath = CreateTempFile(".onnx", "onnx-test");

    public FakeVictimRegistryHandler RegistryHandler { get; } = new();
    public FakeBiometricTemplateExtractor BiometricExtractor { get; } = new();
    public FakeCameraLivePreviewPort WorkerPreviewPort { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GuardiaoDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<GuardiaoDbContext>(options =>
                options.UseInMemoryDatabase("GuardiaoIntegrationTests"));

            services.AddSingleton<HttpMessageHandler>(RegistryHandler);
            services.RemoveAll<IBiometricTemplateExtractor>();
            services.AddSingleton<IBiometricTemplateExtractor>(BiometricExtractor);
            services.RemoveAll<ICameraLivePreviewPort>();
            services.AddSingleton<ICameraLivePreviewPort>(WorkerPreviewPort);
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=guardiao_tests;Username=guardiao;Password=guardiao",
                ["VictimRegistry:BaseUrl"] = "https://registry.test",
                ["VictimRegistry:StaticAccessToken"] = "test-token",
                ["VictimRegistry:WebhookSecret"] = WebhookSecret,
                ["VictimRegistry:AllowedClockSkewSeconds"] = "300",
                ["VictimRegistry:ReconciliationIntervalSeconds"] = "3600",
                ["VictimRegistry:ReconciliationPageSize"] = "100",
                ["VictimRegistry:InitialLookbackMinutes"] = "5",
                ["ApiSecurity:EnableDebugHeaderAuthentication"] = "true",
                ["ApiSecurity:PanelSharedSecret"] = "panel-test-secret",
                ["ApiSecurity:WorkerSharedSecret"] = "worker-test-secret",
                ["WorkerPreview:BaseUrl"] = "https://worker-preview.test",
                ["WorkerPreview:RequestTimeoutSeconds"] = "2",
                ["ApiSecurity:EnableSwaggerUi"] = "false",
                ["BiometricProcessing:DetectionModelPath"] = DetectionModelPath,
                ["BiometricProcessing:EmbeddingModelPath"] = EmbeddingModelPath,
                ["OperationalNotifications:EnableWebhook"] = "true",
                ["OperationalNotifications:WebhookUrl"] = "https://notifications.test/guardiao/incidents",
                ["OperationalNotifications:WebhookSecret"] = "notify-test-secret",
                ["OperationalNotifications:EnableSmtp"] = "false",
                ["OperationalNotifications:DeliveryTimeoutSeconds"] = "2",
                ["OperationalNotifications:RetryAttempts"] = "3",
                ["OperationalNotifications:InitialRetryDelayMilliseconds"] = "1",
                ["OperationalNotifications:EnableEscalation"] = "true",
                ["OperationalNotifications:EscalationWindowMinutes"] = "5",
                ["OperationalNotifications:EscalationScanIntervalSeconds"] = "60",
                ["ApiSecurity:MaxApiRequestBodyBytes"] = "1048576",
                ["ApiSecurity:MaxWebhookRequestBodyBytes"] = "16384",
                ["ApiSecurity:ApiWriteRateLimitPermitLimit"] = "100",
                ["ApiSecurity:ApiWriteRateLimitWindowSeconds"] = "60",
                ["ApiSecurity:WebhookRateLimitPermitLimit"] = "100",
                ["ApiSecurity:WebhookRateLimitWindowSeconds"] = "60"
            });
        });
    }

    private static string CreateTempFile(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }
}

public sealed class FakeCameraLivePreviewPort : ICameraLivePreviewPort
{
    private readonly Dictionary<Guid, CameraLivePreviewPayload> _frames = [];

    public void SetPreview(Guid cameraId, byte[] content, string contentType, DateTime capturedAtUtc, long sequence = 1)
    {
        _frames[cameraId] = new CameraLivePreviewPayload(content, contentType, capturedAtUtc, sequence);
    }

    public Task<CameraLivePreviewPayload?> GetLatestPreviewAsync(Guid cameraId, CancellationToken cancellationToken = default)
    {
        _frames.TryGetValue(cameraId, out var payload);
        return Task.FromResult(payload);
    }

    public Task<CameraLivePreviewStreamPayload?> OpenLivePreviewStreamAsync(Guid cameraId, CancellationToken cancellationToken = default)
    {
        if (!_frames.TryGetValue(cameraId, out var payload))
        {
            return Task.FromResult<CameraLivePreviewStreamPayload?>(null);
        }

        var boundary = "guardiao-frame";
        var header = $"--{boundary}\r\nContent-Type: {payload.ContentType}\r\nContent-Length: {payload.Content.Length}\r\n\r\n";
        var footer = "\r\n";
        var bytes = Encoding.ASCII.GetBytes(header)
            .Concat(payload.Content)
            .Concat(Encoding.ASCII.GetBytes(footer))
            .ToArray();

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(bytes))
        };
        response.Content.Headers.ContentType = new("multipart/x-mixed-replace")
        {
            Parameters = { new System.Net.Http.Headers.NameValueHeaderValue("boundary", boundary) }
        };
        var stream = new MemoryStream(bytes);

        var streamPayload = new CameraLivePreviewStreamPayload(
            response,
            stream,
            response.Content.Headers.ContentType.ToString());

        return Task.FromResult<CameraLivePreviewStreamPayload?>(streamPayload);
    }
}

public sealed class FakeBiometricTemplateExtractor : IBiometricTemplateExtractor
{
    public bool ForceNoFace { get; set; }
    public bool ForceMultipleFaces { get; set; }
    public float[] Embedding { get; set; } = [0.1f, 0.2f, 0.3f, 0.4f];

    public Task<BiometricExtractionResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        if (ForceNoFace)
        {
            throw new InvalidDataException("No face was detected in the uploaded image.");
        }

        if (ForceMultipleFaces)
        {
            throw new InvalidDataException("The uploaded image must contain a single face.");
        }

        return Task.FromResult(new BiometricExtractionResult(Embedding, 1));
    }
}

public sealed class FakeVictimRegistryHandler : HttpMessageHandler
{
    private readonly Dictionary<string, object> _cases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<HttpStatusCode> _webhookResponses = new();

    public List<string> WebhookPayloads { get; } = [];

    public void SetCase(string externalCaseId, object payload)
    {
        _cases[externalCaseId] = payload;
    }

    public void EnqueueWebhookResponse(HttpStatusCode statusCode)
    {
        _webhookResponses.Enqueue(statusCode);
    }

    public void ResetWebhook()
    {
        WebhookPayloads.Clear();
        _webhookResponses.Clear();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Missing request URI.");
        }

        if (request.RequestUri.Host.Equals("notifications.test", StringComparison.OrdinalIgnoreCase))
        {
            var payload = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            WebhookPayloads.Add(payload);

            var statusCode = _webhookResponses.Count > 0
                ? _webhookResponses.Dequeue()
                : HttpStatusCode.OK;

            return new HttpResponseMessage(statusCode);
        }

        if (request.RequestUri.AbsolutePath.StartsWith("/api/v1/cases/") &&
            !request.RequestUri.AbsolutePath.EndsWith("/media", StringComparison.OrdinalIgnoreCase) &&
            !request.RequestUri.AbsolutePath.Contains("/download", StringComparison.OrdinalIgnoreCase))
        {
            var externalCaseId = request.RequestUri.AbsolutePath.Split('/').Last();
            if (_cases.TryGetValue(externalCaseId, out var payload))
            {
                return JsonResponse(payload);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (request.RequestUri.AbsolutePath == "/api/v1/cases")
        {
            return JsonResponse(new { items = Array.Empty<object>() });
        }

        if (request.RequestUri.AbsolutePath.EndsWith("/media", StringComparison.OrdinalIgnoreCase))
        {
            return JsonResponse(Array.Empty<object>());
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage JsonResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
    }
}

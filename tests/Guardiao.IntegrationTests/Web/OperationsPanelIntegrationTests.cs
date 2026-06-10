using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Guardiao.Web;
using Guardiao.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Web;

public class OperationsPanelIntegrationTests
{
    [Fact]
    public async Task GetLogin_ShouldRenderOperationsPanelEntry()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/login");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Centro de operacoes", html);
    }

    [Fact]
    public async Task PostLogin_ShouldRedirectToDashboard_WhenCredentialsAreValid()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var response = await client.PostAsync(
            "/operations/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["userName"] = "operator.ana",
                ["role"] = "operator"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostLogout_ShouldClearSessionAndReturnOperatorToLoginFlow()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        await AuthenticateAsync(client, "operator.ana", "operator");

        using var logoutResponse = await client.PostAsync("/operations/logout", content: null);

        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/login", logoutResponse.Headers.Location?.OriginalString);

        using var rootResponse = await client.GetAsync("/");
        var rootHtml = await rootResponse.Content.ReadAsStringAsync();

        rootResponse.EnsureSuccessStatusCode();
        Assert.Contains("Acesso negado", rootHtml);
    }

    [Fact]
    public async Task GetRoot_ShouldRenderAuthorizationAwareMessage_WhenUnauthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Acesso negado", html);
    }

    [Fact]
    public async Task GetIncidents_ShouldRenderIncident_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        factory.ApiHandler.Incidents.Add(new IncidentState
        {
            Id = Guid.NewGuid(),
            ProtectedCaseId = Guid.NewGuid(),
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/incidents");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Incidentes", html);
        Assert.Contains("PendingReview", html);
        Assert.Contains("Abrir", html);
    }

    [Fact]
    public async Task GetCases_ShouldRenderOperationalClassificationAndRulesLink_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        factory.ApiHandler.Cases.Add(new ProtectedCaseState
        {
            Id = Guid.NewGuid(),
            ExternalCaseId = "case-operator-flow",
            SubjectRole = "ProtectedWoman",
            Version = 3,
            MonitoringStatus = "enabled",
            ConsentStatus = "granted",
            PersonProjectionId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-20),
            LastSynchronizedAt = DateTime.UtcNow,
            LastSyncStatus = "ok"
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/cases");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Casos monitorados", html);
        Assert.Contains("Mulher protegida", html);
        Assert.Contains("Regras", html);
    }

    [Fact]
    public async Task GetCaseDetail_ShouldRenderClassificationBiometricsAndRules_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var caseId = Guid.NewGuid();
        var personProjectionId = Guid.NewGuid();
        factory.ApiHandler.Cases.Add(new ProtectedCaseState
        {
            Id = caseId,
            ExternalCaseId = "case-detail-flow",
            SubjectRole = "Aggressor",
            Version = 4,
            MonitoringStatus = "enabled",
            ConsentStatus = "granted",
            PersonProjectionId = personProjectionId,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            LastSynchronizedAt = DateTime.UtcNow,
            LastSyncStatus = "ok"
        });
        factory.ApiHandler.Rules.Add(new MonitoringRuleState
        {
            Id = Guid.NewGuid(),
            ProtectedCaseId = caseId,
            SiteId = Guid.NewGuid(),
            CameraId = Guid.NewGuid(),
            StartsAt = new TimeOnly(8, 0),
            EndsAt = new TimeOnly(18, 0),
            IsEnabled = true
        });
        factory.ApiHandler.BiometricTemplates.Add(new BiometricTemplateState
        {
            Id = Guid.NewGuid(),
            PersonProjectionId = personProjectionId,
            ExternalPersonId = "person-detail-flow",
            Source = "panel_upload",
            DisplayName = "agressor-01.jpg",
            ContentType = "image/jpeg",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync($"/cases/{caseId}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Classificacao operacional", html);
        Assert.Contains("Agressor monitorado", html);
        Assert.Contains("Biometria cadastrada", html);
        Assert.Contains("Salvar regra", html);
    }

    [Fact]
    public async Task GetCameras_ShouldRenderSitesCameraStateAndToggle_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var siteId = Guid.NewGuid();
        factory.ApiHandler.Sites.Add(new SiteState
        {
            Id = siteId,
            InstitutionId = Guid.NewGuid(),
            Name = "Campus Norte",
            AddressLine = "Bloco C"
        });
        factory.ApiHandler.Cameras.Add(new CameraState
        {
            Id = Guid.NewGuid(),
            SiteId = siteId,
            Name = "Entrada principal",
            StreamEndpoint = "webcam://0",
            IsEnabled = true
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/cameras");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Cameras e sites", html);
        Assert.Contains("Campus Norte", html);
        Assert.Contains("Entrada principal", html);
        Assert.Contains("Desabilitar", html);
    }

    [Fact]
    public async Task GetIncidentDetail_ShouldRenderHumanValidationActions_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var incident = new IncidentState
        {
            Id = Guid.NewGuid(),
            ProtectedCaseId = Guid.NewGuid(),
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow
        };
        factory.ApiHandler.Incidents.Add(incident);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync($"/incidents/{incident.Id}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Validacao humana", html);
        Assert.Contains("Confirmar", html);
        Assert.Contains("Descartar", html);
    }

    [Fact]
    public async Task GetIncidentDetail_ShouldRenderEscalationAndNotificationHistory_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var incident = new IncidentState
        {
            Id = Guid.NewGuid(),
            ProtectedCaseId = Guid.NewGuid(),
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            EscalatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        factory.ApiHandler.Incidents.Add(incident);
        factory.ApiHandler.IncidentNotifications.Add(new IncidentNotificationState
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            EventType = "incident.escalated",
            Channel = "webhook",
            DeliveryStatus = "Sent",
            AttemptCount = 2,
            HasEvidence = true,
            Details = "status=PendingReview;has_evidence=true",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync($"/incidents/{incident.Id}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Escalonado", html);
        Assert.Contains("Notificacoes operacionais", html);
        Assert.Contains("incident.escalated", html);
        Assert.Contains("webhook", html);
    }

    [Fact]
    public async Task GetIncidentDetail_ShouldRenderEvidenceSection_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var incident = new IncidentState
        {
            Id = Guid.NewGuid(),
            ProtectedCaseId = Guid.NewGuid(),
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow
        };
        factory.ApiHandler.Incidents.Add(incident);
        factory.ApiHandler.IncidentEvidences.Add(new IncidentEvidenceState
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            CandidateEventId = incident.CandidateEventId,
            ArtifactType = "Snapshot",
            ContentType = "image/jpeg",
            CreatedAtUtc = DateTime.UtcNow,
            Content = [1, 2, 3, 4]
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync($"/incidents/{incident.Id}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Evidencias", html);
        Assert.Contains("Snapshot", html);
        Assert.Contains("data:image/jpeg;base64", html);
    }

    [Fact]
    public async Task GetDashboard_ShouldRenderSummaryAndRecentItems_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory();
        var cameraId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var incidentId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        factory.ApiHandler.Incidents.Add(new IncidentState
        {
            Id = incidentId,
            ProtectedCaseId = caseId,
            CandidateEventId = Guid.NewGuid(),
            Status = "PendingReview",
            CreatedAtUtc = DateTime.UtcNow
        });
        factory.ApiHandler.Cases.Add(new ProtectedCaseState
        {
            Id = caseId,
            ExternalCaseId = "case-dashboard",
            SubjectRole = "ProtectedWoman",
            Version = 2,
            MonitoringStatus = "enabled",
            ConsentStatus = "granted",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            LastSynchronizedAt = DateTime.UtcNow,
            LastSyncStatus = "ok"
        });
        factory.ApiHandler.Sites.Add(new SiteState
        {
            Id = siteId,
            InstitutionId = Guid.NewGuid(),
            Name = "Campus Central",
            AddressLine = "Bloco A"
        });
        factory.ApiHandler.Cameras.Add(new CameraState
        {
            Id = cameraId,
            SiteId = siteId,
            Name = "Webcam Recepcao",
            StreamEndpoint = "rtsp://camera-01",
            IsEnabled = true
        });
        factory.ApiHandler.AuditEntries.Add(new AuditEntryState
        {
            Id = Guid.NewGuid(),
            ActorType = "Operator",
            Action = "incident.review.confirmed",
            EntityName = "Incident",
            EntityId = Guid.NewGuid().ToString(),
            Details = "status=Confirmed",
            CreatedAtUtc = DateTime.UtcNow
        });
        factory.ApiHandler.CameraViews.Add(new CameraOperationalViewState
        {
            CameraId = cameraId,
            SiteId = siteId,
            CameraName = "Webcam Recepcao",
            SiteName = "Campus Central",
            IsEnabled = true,
            StreamEndpoint = "webcam://0",
            LastDetectionAtUtc = DateTime.UtcNow,
            LatestSnapshot = new EvidencePreviewState
            {
                IncidentId = incidentId,
                EvidenceId = evidenceId,
                ContentType = "image/jpeg",
                CapturedAtUtc = DateTime.UtcNow
            },
            ActiveAlert = new AggressorPresenceAlertState
            {
                ProtectedCaseId = Guid.NewGuid(),
                FullName = "Agressor de teste",
                MatchScore = 0.97,
                DetectedAtUtc = DateTime.UtcNow,
                Snapshot = new EvidencePreviewState
                {
                    IncidentId = incidentId,
                    EvidenceId = evidenceId,
                    ContentType = "image/jpeg",
                    CapturedAtUtc = DateTime.UtcNow
                }
            }
        });
        factory.ApiHandler.CameraViews[0].RecentProtectedWomen.Add(new DetectedSubjectState
        {
            ProtectedCaseId = caseId,
            PersonProjectionId = Guid.NewGuid(),
            FullName = "Vitima Piloto",
            SubjectRole = "ProtectedWoman",
            MatchScore = 0.94,
            DetectedAtUtc = DateTime.UtcNow,
            IncidentStatus = "PendingReview",
            Snapshot = new EvidencePreviewState
            {
                IncidentId = incidentId,
                EvidenceId = evidenceId,
                ContentType = "image/jpeg",
                CapturedAtUtc = DateTime.UtcNow
            }
        });
        factory.ApiHandler.CameraViews[0].ActiveAlert!.NearbyProtectedWomen.Add("Vitima Piloto");
        factory.ApiHandler.IncidentEvidences.Add(new IncidentEvidenceState
        {
            Id = evidenceId,
            IncidentId = incidentId,
            ArtifactType = "Snapshot",
            ContentType = "image/jpeg",
            CreatedAtUtc = DateTime.UtcNow,
            Content = [1, 2, 3]
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Resumo operacional", html);
        Assert.Contains("Casos monitorados", html);
        Assert.Contains("Webcam Recepcao", html);
        Assert.Contains("Mulheres detectadas no ambiente", html);
        Assert.Contains("Agressor de teste", html);
        Assert.Contains("incident.review.confirmed", html);
        Assert.Contains("Em revisao", html);
    }

    [Fact]
    public async Task GetDashboard_ShouldRenderOperationalEmptyState_WhenNoCameraViewsAreAvailable()
    {
        using var factory = new GuardiaoWebFactory();
        factory.ApiHandler.Cases.Add(new ProtectedCaseState
        {
            Id = Guid.NewGuid(),
            ExternalCaseId = "case-empty-dashboard",
            SubjectRole = "ProtectedWoman",
            Version = 1,
            MonitoringStatus = "enabled",
            ConsentStatus = "granted",
            CreatedAt = DateTime.UtcNow,
            LastSynchronizedAt = DateTime.UtcNow,
            LastSyncStatus = "ok"
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Nenhuma camera monitorada disponivel.", html);
        Assert.Contains("ultima leitura de cada ambiente", html);
    }

    [Fact]
    public async Task GetReady_ShouldReturnReady_WhenOperationsApiHeadersWork()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/ready");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Ready", body);
    }

    [Fact]
    public async Task GetAudit_ShouldRenderTrailEntries_ForOperator()
    {
        using var factory = new GuardiaoWebFactory();
        factory.ApiHandler.AuditEntries.Add(new AuditEntryState
        {
            Id = Guid.NewGuid(),
            ActorType = "Operator",
            Action = "monitoring_rule.updated",
            EntityName = "MonitoringRule",
            EntityId = Guid.NewGuid().ToString(),
            Details = "enabled=true",
            CreatedAtUtc = DateTime.UtcNow
        });

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "operator.ana", "operator");

        var response = await client.GetAsync("/audit");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Trilha de auditoria", html);
        Assert.Contains("monitoring_rule.updated", html);
    }

    internal static async Task AuthenticateAsync(HttpClient client, string userName, string role)
    {
        using var response = await client.PostAsync(
            "/operations/login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["userName"] = userName,
                ["role"] = role
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public sealed class GuardiaoWebFactory : WebApplicationFactory<WebEntryPoint>
{
    private readonly string? _userName;
    private readonly string? _role;

    public FakeOperationsApiHandler ApiHandler { get; } = new();

    public GuardiaoWebFactory(string? userName = null, string? role = null)
    {
        _userName = userName;
        _role = role;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<HttpMessageHandler>(ApiHandler);

            if (!string.IsNullOrWhiteSpace(_userName) && !string.IsNullOrWhiteSpace(_role))
            {
                services.AddScoped<AuthenticationStateProvider>(_ =>
                    new TestAuthenticationStateProvider(_userName, _role));
            }
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebSecurity:EnableOperationsDemoLogin"] = "true",
                ["PanelApi:BaseUrl"] = "https://operations-api.test",
                ["PanelApi:SharedSecret"] = FakeOperationsApiHandler.SharedSecret
            });
        });
    }
}

internal sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly AuthenticationState _state;

    public TestAuthenticationStateProvider(string userName, string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, userName),
            new Claim(ClaimTypes.Role, role)
        ], "test");

        _state = new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_state);
}

public sealed class FakeOperationsApiHandler : HttpMessageHandler
{
    public const string SharedSecret = "test-panel-secret";
    private static readonly byte[] SampleEvidenceBytes = [0x47, 0x55, 0x41, 0x52, 0x44];

    public List<IncidentState> Incidents { get; } = [];
    public List<ProtectedCaseState> Cases { get; } = [];
    public List<MonitoringRuleState> Rules { get; } = [];
    public List<SiteState> Sites { get; } = [];
    public List<CameraState> Cameras { get; } = [];
    public List<CameraOperationalViewState> CameraViews { get; } = [];
    public List<AuditEntryState> AuditEntries { get; } = [];
    public List<BiometricTemplateState> BiometricTemplates { get; } = [];
    public List<IncidentEvidenceState> IncidentEvidences { get; } = [];
    public List<IncidentNotificationState> IncidentNotifications { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(request))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (request.Method == HttpMethod.Get && path == "/api/operations/summary")
        {
            var cameraViews = CameraViews.Count > 0
                ? CameraViews.Select(x => x.ToModel()).ToArray()
                : Cameras.OrderBy(x => x.Name)
                    .Select(x => new CameraOperationalViewModel(
                        x.Id,
                        x.SiteId,
                        x.Name,
                        Sites.FirstOrDefault(site => site.Id == x.SiteId)?.Name ?? "Site nao configurado",
                        x.IsEnabled,
                        x.StreamEndpoint,
                        null,
                        null,
                        [],
                        null))
                    .ToArray();

            return Json(new OperationsSummaryModel(
                Incidents.Count,
                Cases.Count,
                Cameras.Count,
                AuditEntries.Count,
                Incidents.OrderByDescending(x => x.CreatedAtUtc).Take(5).Select(x => x.ToRecentModel()).ToArray(),
                AuditEntries.OrderByDescending(x => x.CreatedAtUtc).Take(5).Select(x => x.ToModel()).ToArray(),
                cameraViews));
        }

        if (request.Method == HttpMethod.Get && path == "/api/incidents")
        {
            return Json(Incidents.OrderByDescending(x => x.CreatedAtUtc).Select(x => x.ToListItemModel()).ToArray());
        }

        if (request.Method == HttpMethod.Get && segments.Length == 3 && segments[0] == "api" && segments[1] == "incidents")
        {
            var id = Guid.Parse(segments[2]);
            var incident = Incidents.FirstOrDefault(x => x.Id == id);
            return incident is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(incident.ToDetailModel());
        }

        if (request.Method == HttpMethod.Get && segments.Length == 4 && segments[0] == "api" && segments[1] == "incidents" && segments[3] == "evidences")
        {
            var incidentId = Guid.Parse(segments[2]);
            return Json(IncidentEvidences
                .Where(x => x.IncidentId == incidentId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => x.ToModel())
                .ToArray());
        }

        if (request.Method == HttpMethod.Get && segments.Length == 6 && segments[0] == "api" && segments[1] == "incidents" && segments[3] == "evidences" && segments[5] == "content")
        {
            var evidenceId = Guid.Parse(segments[4]);
            var evidence = IncidentEvidences.FirstOrDefault(x => x.Id == evidenceId);
            if (evidence is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var content = new ByteArrayContent(evidence.Content.Length == 0 ? SampleEvidenceBytes : evidence.Content);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(evidence.ContentType);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        }

        if (request.Method == HttpMethod.Get && segments.Length == 4 && segments[0] == "api" && segments[1] == "incidents" && segments[3] == "notifications")
        {
            var incidentId = Guid.Parse(segments[2]);
            return Json(IncidentNotifications
                .Where(x => x.IncidentId == incidentId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => x.ToModel())
                .ToArray());
        }

        if (request.Method == HttpMethod.Post && segments.Length == 5 && segments[0] == "api" && segments[1] == "incidents" && segments[3] == "review")
        {
            var id = Guid.Parse(segments[2]);
            var incident = Incidents.First(x => x.Id == id);
            var body = await request.Content!.ReadFromJsonAsync<TestIncidentReviewRequest>(cancellationToken: cancellationToken)
                ?? new TestIncidentReviewRequest();
            incident.ReviewNotes = body.ReviewNotes;
            incident.ReviewedAtUtc = DateTime.UtcNow;
            incident.Status = segments[4] == "confirm" ? "Confirmed" : "Dismissed";

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = segments[4] == "confirm" ? "incident.review.confirmed" : "incident.review.dismissed",
                EntityName = "Incident",
                EntityId = incident.Id.ToString(),
                Details = body.ReviewNotes,
                CreatedAtUtc = DateTime.UtcNow
            });

            return Json(incident.ToDetailModel());
        }

        if (request.Method == HttpMethod.Get && path == "/api/cases")
        {
            return Json(Cases.OrderByDescending(x => x.CreatedAt).Select(x => x.ToListItemModel()).ToArray());
        }

        if (request.Method == HttpMethod.Get && segments.Length == 3 && segments[0] == "api" && segments[1] == "cases")
        {
            var id = Guid.Parse(segments[2]);
            var item = Cases.FirstOrDefault(x => x.Id == id);
            return item is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(item.ToDetailModel());
        }

        if (request.Method == HttpMethod.Put && segments.Length == 4 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "subject-role")
        {
            var caseId = Guid.Parse(segments[2]);
            var body = await request.Content!.ReadFromJsonAsync<TestUpdateProtectedCaseSubjectRoleRequest>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Missing subject role request body.");
            var item = Cases.First(x => x.Id == caseId);
            item.SubjectRole = body.SubjectRole;

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = "protected_case.subject_role.updated",
                EntityName = "ProtectedCase",
                EntityId = item.Id.ToString(),
                Details = $"subject_role={body.SubjectRole}",
                CreatedAtUtc = DateTime.UtcNow
            });

            return Json(item.ToDetailModel());
        }

        if (request.Method == HttpMethod.Get && segments.Length == 4 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "biometrics")
        {
            var caseId = Guid.Parse(segments[2]);
            var item = Cases.FirstOrDefault(x => x.Id == caseId);
            if (item is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return Json(BiometricTemplates
                .Where(x => x.PersonProjectionId == item.PersonProjectionId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => x.ToModel())
                .ToArray());
        }

        if (request.Method == HttpMethod.Post && segments.Length == 4 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "biometrics")
        {
            var caseId = Guid.Parse(segments[2]);
            var item = Cases.FirstOrDefault(x => x.Id == caseId);
            if (item is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            var template = new BiometricTemplateState
            {
                Id = Guid.NewGuid(),
                PersonProjectionId = item.PersonProjectionId,
                ExternalPersonId = "person-upload",
                Source = "panel_upload",
                DisplayName = "uploaded-face.png",
                ContentType = "image/png",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            BiometricTemplates.Add(template);

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = "biometric_template.created",
                EntityName = "BiometricTemplate",
                EntityId = template.Id.ToString(),
                Details = $"case_id={caseId}",
                CreatedAtUtc = DateTime.UtcNow
            });

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(JsonSerializer.Serialize(template.ToUploadModel()), Encoding.UTF8, "application/json")
            };
        }

        if (request.Method == HttpMethod.Delete && segments.Length == 5 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "biometrics")
        {
            var templateId = Guid.Parse(segments[4]);
            var template = BiometricTemplates.FirstOrDefault(x => x.Id == templateId);
            if (template is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            template.IsActive = false;
            template.DeactivatedAtUtc = DateTime.UtcNow;

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = "biometric_template.deactivated",
                EntityName = "BiometricTemplate",
                EntityId = template.Id.ToString(),
                Details = "active=false",
                CreatedAtUtc = DateTime.UtcNow
            });

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (request.Method == HttpMethod.Get && segments.Length == 4 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "rules")
        {
            var caseId = Guid.Parse(segments[2]);
            return Json(Rules.Where(x => x.ProtectedCaseId == caseId).Select(x => x.ToModel()).ToArray());
        }

        if (request.Method == HttpMethod.Put && segments.Length == 5 && segments[0] == "api" && segments[1] == "cases" && segments[3] == "rules")
        {
            var ruleId = Guid.Parse(segments[4]);
            var body = await request.Content!.ReadFromJsonAsync<TestUpdateMonitoringRuleRequest>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Missing rule request body.");
            var rule = Rules.First(x => x.Id == ruleId);
            rule.SiteId = body.SiteId;
            rule.CameraId = body.CameraId;
            rule.StartsAt = body.StartsAt;
            rule.EndsAt = body.EndsAt;
            rule.IsEnabled = body.IsEnabled;

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = "monitoring_rule.updated",
                EntityName = "MonitoringRule",
                EntityId = rule.Id.ToString(),
                Details = $"enabled={body.IsEnabled}",
                CreatedAtUtc = DateTime.UtcNow
            });

            return Json(rule.ToModel());
        }

        if (request.Method == HttpMethod.Get && path == "/api/sites")
        {
            return Json(Sites.Select(x => x.ToModel()).ToArray());
        }

        if (request.Method == HttpMethod.Get && path == "/api/cameras")
        {
            return Json(Cameras.OrderBy(x => x.Name).Select(x => x.ToModel()).ToArray());
        }

        if (request.Method == HttpMethod.Put && segments.Length == 4 && segments[0] == "api" && segments[1] == "cameras" && segments[3] == "state")
        {
            var cameraId = Guid.Parse(segments[2]);
            var body = await request.Content!.ReadFromJsonAsync<TestUpdateCameraStateRequest>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Missing camera state request body.");
            var camera = Cameras.First(x => x.Id == cameraId);
            camera.IsEnabled = body.IsEnabled;

            AuditEntries.Add(new AuditEntryState
            {
                Id = Guid.NewGuid(),
                ActorType = "Operator",
                Action = "camera.state.updated",
                EntityName = "Camera",
                EntityId = camera.Id.ToString(),
                Details = $"enabled={body.IsEnabled}",
                CreatedAtUtc = DateTime.UtcNow
            });

            return Json(camera.ToModel());
        }

        if (request.Method == HttpMethod.Get && path == "/api/audit")
        {
            return Json(AuditEntries.OrderByDescending(x => x.CreatedAtUtc).Select(x => x.ToModel()).ToArray());
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private static bool IsAuthorized(HttpRequestMessage request)
    {
        var panelUser = request.Headers.TryGetValues("X-Panel-User", out var users) ? users.SingleOrDefault() : null;
        var panelRole = request.Headers.TryGetValues("X-Panel-Role", out var roles) ? roles.SingleOrDefault() : null;
        var panelSecret = request.Headers.TryGetValues("X-Panel-Auth", out var secrets) ? secrets.SingleOrDefault() : null;

        return !string.IsNullOrWhiteSpace(panelUser) &&
               !string.IsNullOrWhiteSpace(panelRole) &&
               string.Equals(panelSecret, SharedSecret, StringComparison.Ordinal);
    }
}

public sealed class IncidentState
{
    public Guid Id { get; set; }
    public Guid ProtectedCaseId { get; set; }
    public Guid CandidateEventId { get; set; }
    public string Status { get; set; } = "PendingReview";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }

    public IncidentListItemModel ToListItemModel()
        => new(Id, ProtectedCaseId, CandidateEventId, Status, CreatedAtUtc, ReviewedAtUtc, EscalatedAtUtc);

    public IncidentDetailModel ToDetailModel()
        => new(Id, ProtectedCaseId, CandidateEventId, Status, CreatedAtUtc, ReviewedAtUtc, EscalatedAtUtc, ReviewNotes);

    public RecentIncidentModel ToRecentModel()
        => new(Id, ProtectedCaseId, Status, CreatedAtUtc);
}

public sealed class IncidentEvidenceState
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public Guid? CandidateEventId { get; set; }
    public string ArtifactType { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    public DateTime CreatedAtUtc { get; set; }
    public byte[] Content { get; set; } = [];

    public IncidentEvidenceModel ToModel()
        => new(Id, IncidentId, CandidateEventId, ArtifactType, ContentType, CreatedAtUtc);
}

public sealed class IncidentNotificationState
{
    public Guid Id { get; set; }
    public Guid IncidentId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public bool HasEvidence { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public IncidentNotificationModel ToModel()
        => new(Id, IncidentId, EventType, Channel, DeliveryStatus, AttemptCount, HasEvidence, Details, CreatedAtUtc, CompletedAtUtc);
}

public sealed class ProtectedCaseState
{
    public Guid Id { get; set; }
    public string ExternalCaseId { get; set; } = string.Empty;
    public string SubjectRole { get; set; } = "ProtectedWoman";
    public long Version { get; set; }
    public string MonitoringStatus { get; set; } = string.Empty;
    public string ConsentStatus { get; set; } = string.Empty;
    public Guid PersonProjectionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSynchronizedAt { get; set; }
    public string LastSyncStatus { get; set; } = string.Empty;
    public string? LastSyncFailureReason { get; set; }

    public ProtectedCaseListItemModel ToListItemModel()
        => new(Id, ExternalCaseId, SubjectRole, Version, MonitoringStatus, ConsentStatus, LastSynchronizedAt, LastSyncStatus);

    public ProtectedCaseDetailModel ToDetailModel()
        => new(Id, ExternalCaseId, SubjectRole, Version, MonitoringStatus, ConsentStatus, PersonProjectionId, CreatedAt, LastSynchronizedAt, LastSyncStatus, LastSyncFailureReason);
}

public sealed class CameraOperationalViewState
{
    public Guid CameraId { get; set; }
    public Guid SiteId { get; set; }
    public string CameraName { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string StreamEndpoint { get; set; } = string.Empty;
    public DateTime? LastDetectionAtUtc { get; set; }
    public EvidencePreviewState? LatestSnapshot { get; set; }
    public List<DetectedSubjectState> RecentProtectedWomen { get; } = [];
    public AggressorPresenceAlertState? ActiveAlert { get; set; }

    public CameraOperationalViewModel ToModel()
        => new(
            CameraId,
            SiteId,
            CameraName,
            SiteName,
            IsEnabled,
            StreamEndpoint,
            LastDetectionAtUtc,
            LatestSnapshot?.ToModel(),
            RecentProtectedWomen.Select(x => x.ToModel()).ToArray(),
            ActiveAlert?.ToModel());
}

public sealed class EvidencePreviewState
{
    public Guid IncidentId { get; set; }
    public Guid EvidenceId { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public DateTime CapturedAtUtc { get; set; }

    public EvidencePreviewModel ToModel()
        => new(IncidentId, EvidenceId, ContentType, CapturedAtUtc);
}

public sealed class DetectedSubjectState
{
    public Guid ProtectedCaseId { get; set; }
    public Guid PersonProjectionId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string SubjectRole { get; set; } = "ProtectedWoman";
    public bool IsBystander { get; set; }
    public string IncidentStatus { get; set; } = "PendingReview";
    public double MatchScore { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public EvidencePreviewState? Snapshot { get; set; }

    public DetectedSubjectModel ToModel()
        => new(ProtectedCaseId, PersonProjectionId, FullName, SubjectRole, IsBystander, IncidentStatus, MatchScore, DetectedAtUtc, Snapshot?.ToModel());
}

public sealed class AggressorPresenceAlertState
{
    public Guid ProtectedCaseId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public double MatchScore { get; set; }
    public DateTime DetectedAtUtc { get; set; }
    public List<string> NearbyProtectedWomen { get; } = [];
    public EvidencePreviewState? Snapshot { get; set; }

    public AggressorPresenceAlertModel ToModel()
        => new(ProtectedCaseId, FullName, MatchScore, DetectedAtUtc, NearbyProtectedWomen.ToArray(), Snapshot?.ToModel());
}

public sealed class MonitoringRuleState
{
    public Guid Id { get; set; }
    public Guid ProtectedCaseId { get; set; }
    public Guid SiteId { get; set; }
    public Guid CameraId { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public bool IsEnabled { get; set; }

    public MonitoringRuleModel ToModel()
        => new(Id, SiteId, CameraId, StartsAt, EndsAt, IsEnabled);
}

public sealed class BiometricTemplateState
{
    public Guid Id { get; set; }
    public Guid PersonProjectionId { get; set; }
    public string ExternalPersonId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }

    public BiometricTemplateModel ToModel()
        => new(Id, PersonProjectionId, ExternalPersonId, Source, DisplayName, ContentType, IsActive, CreatedAt, DeactivatedAtUtc);

    public BiometricTemplateUploadModel ToUploadModel()
        => new(Id, PersonProjectionId, ExternalPersonId, DisplayName, ContentType, CreatedAt);
}

public sealed class SiteState
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;

    public SiteModel ToModel()
        => new(Id, InstitutionId, Name, AddressLine);
}

public sealed class CameraState
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StreamEndpoint { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }

    public CameraModel ToModel()
        => new(Id, SiteId, Name, StreamEndpoint, IsEnabled);
}

public sealed class AuditEntryState
{
    public Guid Id { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }

    public AuditEntryModel ToModel()
        => new(Id, ActorType, Action, EntityName, EntityId, Details, CreatedAtUtc);
}

internal sealed class TestIncidentReviewRequest
{
    public string ReviewNotes { get; set; } = string.Empty;
}

internal sealed class TestUpdateMonitoringRuleRequest
{
    public Guid SiteId { get; set; }
    public Guid CameraId { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public bool IsEnabled { get; set; }
}

internal sealed class TestUpdateCameraStateRequest
{
    public bool IsEnabled { get; set; }
}

internal sealed class TestUpdateProtectedCaseSubjectRoleRequest
{
    public string SubjectRole { get; set; } = string.Empty;
}

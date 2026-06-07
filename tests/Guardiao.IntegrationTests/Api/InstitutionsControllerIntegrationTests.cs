using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using Guardiao.Api;
using Guardiao.Api.Contracts;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        db.Incidents.Add(new Incident(Guid.NewGuid(), Guid.NewGuid()));
        db.ProtectedCases.Add(new ProtectedCase(
            new ExternalCaseId("case-summary"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted));
        db.Cameras.Add(new Camera(Guid.NewGuid(), "Camera Summary", "rtsp://summary"));
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
}

public class GuardiaoApiFactory : WebApplicationFactory<ApiEntryPoint>
{
    public const string WebhookSecret = "integration-secret";

    public FakeVictimRegistryHandler RegistryHandler { get; } = new();

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
                ["ApiSecurity:EnableSwaggerUi"] = "false",
                ["ApiSecurity:MaxApiRequestBodyBytes"] = "65536",
                ["ApiSecurity:MaxWebhookRequestBodyBytes"] = "16384",
                ["ApiSecurity:ApiWriteRateLimitPermitLimit"] = "100",
                ["ApiSecurity:ApiWriteRateLimitWindowSeconds"] = "60",
                ["ApiSecurity:WebhookRateLimitPermitLimit"] = "100",
                ["ApiSecurity:WebhookRateLimitWindowSeconds"] = "60"
            });
        });
    }
}

public sealed class FakeVictimRegistryHandler : HttpMessageHandler
{
    private readonly Dictionary<string, object> _cases = new(StringComparer.OrdinalIgnoreCase);

    public void SetCase(string externalCaseId, object payload)
    {
        _cases[externalCaseId] = payload;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Missing request URI.");
        }

        if (request.RequestUri.AbsolutePath.StartsWith("/api/v1/cases/") &&
            !request.RequestUri.AbsolutePath.EndsWith("/media", StringComparison.OrdinalIgnoreCase) &&
            !request.RequestUri.AbsolutePath.Contains("/download", StringComparison.OrdinalIgnoreCase))
        {
            var externalCaseId = request.RequestUri.AbsolutePath.Split('/').Last();
            if (_cases.TryGetValue(externalCaseId, out var payload))
            {
                return Task.FromResult(JsonResponse(payload));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        if (request.RequestUri.AbsolutePath == "/api/v1/cases")
        {
            return Task.FromResult(JsonResponse(new { items = Array.Empty<object>() }));
        }

        if (request.RequestUri.AbsolutePath.EndsWith("/media", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(JsonResponse(Array.Empty<object>()));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage JsonResponse(object payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
    }
}

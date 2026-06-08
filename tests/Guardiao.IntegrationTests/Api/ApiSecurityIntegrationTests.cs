using System.Net;
using System.Text;
using Guardiao.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

[Trait("Category", "Security")]
public class ApiSecurityIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly HttpClient _client;
    private readonly GuardiaoApiFactory _factory;

    public ApiSecurityIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostIncidentConfirm_ShouldReturnForbidden_WhenRoleLacksPermission()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Guardiao.Infrastructure.Persistence.GuardiaoDbContext>();
        var incident = new Guardiao.Domain.Entities.Incident(Guid.NewGuid(), Guid.NewGuid());
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        _client.DefaultRequestHeaders.Remove("X-Debug-User");
        _client.DefaultRequestHeaders.Remove("X-Debug-Role");
        _client.DefaultRequestHeaders.Add("X-Debug-User", "admin-user");
        _client.DefaultRequestHeaders.Add("X-Debug-Role", "admin");

        var response = await _client.PostAsync(
            $"/api/incidents/{incident.Id}/review/confirm",
            new StringContent("""{"reviewNotes":"blocked"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_ShouldReturnForbidden_WhenRoleLacksPermission()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User", "viewer-user");
        client.DefaultRequestHeaders.Add("X-Debug-Role", "viewer");

        var response = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_ShouldRejectOversizedPayload()
    {
        var content = new string('a', 20_000);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/victim-registry/webhooks")
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Delivery-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-Event-Type", "case.changed");
        request.Headers.Add("X-Event-Timestamp", DateTimeOffset.UtcNow.ToString("O"));
        request.Headers.Add("X-Signature-SHA256", "bad-signature");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}

 [Trait("Category", "Security")]
public class ApiSecurityStartupTests
{
    [Fact]
    public void CreateClient_ShouldFail_WhenDebugHeaderAuthenticationIsEnabledOutsideDevelopment()
    {
        using var factory = new InvalidApiSecurityFactory();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("EnableDebugHeaderAuthentication", exception.ToString(), StringComparison.Ordinal);
    }
}

internal sealed class InvalidApiSecurityFactory : WebApplicationFactory<ApiEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=guardiao_tests;Username=guardiao;Password=guardiao",
                ["VictimRegistry:BaseUrl"] = "https://registry.test",
                ["VictimRegistry:StaticAccessToken"] = "test-token",
                ["VictimRegistry:WebhookSecret"] = "integration-secret",
                ["VictimRegistry:AllowedClockSkewSeconds"] = "300",
                ["VictimRegistry:ReconciliationIntervalSeconds"] = "3600",
                ["VictimRegistry:ReconciliationPageSize"] = "100",
                ["VictimRegistry:InitialLookbackMinutes"] = "5",
                ["ApiSecurity:PanelSharedSecret"] = "production-panel-secret",
                ["ApiSecurity:WorkerSharedSecret"] = "production-worker-secret",
                ["ApiSecurity:MaxApiRequestBodyBytes"] = "1048576",
                ["ApiSecurity:EnableDebugHeaderAuthentication"] = "true"
            });
        });
    }
}

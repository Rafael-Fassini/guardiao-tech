using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Web;
using Guardiao.Web.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
    public async Task GetIncidents_ShouldRenderSeededIncident_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        await factory.SeedAsync(db =>
        {
            var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
            db.Incidents.Add(incident);
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/incidents");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Incidentes", html);
        Assert.Contains("PendingReview", html);
        Assert.Contains("Abrir", html);
    }

    [Fact]
    public async Task GetIncidentDetail_ShouldRenderHumanValidationActions_WhenAuthenticated()
    {
        using var factory = new GuardiaoWebFactory("operator.ana", "operator");
        var incidentId = Guid.Empty;

        await factory.SeedAsync(db =>
        {
            var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
            db.Incidents.Add(incident);
            incidentId = incident.Id;
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/incidents/{incidentId}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Validacao humana", html);
        Assert.Contains("Confirmar", html);
        Assert.Contains("Descartar", html);
    }

    [Fact]
    public async Task GetAudit_ShouldRenderTrailEntries_ForAuditor()
    {
        using var factory = new GuardiaoWebFactory("auditor.maria", "auditor");
        await factory.SeedAsync(db =>
        {
            db.AuditLogs.Add(new AuditLog(
                Guardiao.Domain.Enums.AuditActorType.Operator,
                "web.rule.updated",
                "MonitoringRule",
                Guid.NewGuid().ToString(),
                "enabled=true"));
        });

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/audit");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Trilha de auditoria", html);
        Assert.Contains("web.rule.updated", html);
    }
}

public class GuardiaoWebFactory : WebApplicationFactory<WebEntryPoint>
{
    private readonly string _databaseName = $"GuardiaoWebTests-{Guid.NewGuid():N}";
    private readonly string? _userName;
    private readonly string? _role;

    public GuardiaoWebFactory(string? userName = null, string? role = null)
    {
        _userName = userName;
        _role = role;
    }

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
                options.UseInMemoryDatabase(_databaseName));

            if (!string.IsNullOrWhiteSpace(_userName) && !string.IsNullOrWhiteSpace(_role))
            {
                var sessionDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(OperationsSession));
                if (sessionDescriptor is not null)
                {
                    services.Remove(sessionDescriptor);
                }

                services.AddScoped(_ =>
                {
                    var session = new OperationsSession();
                    session.Login(_userName, _role);
                    return session;
                });
            }
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WebSecurity:EnableOperationsDemoLogin"] = "true"
            });
        });
    }

    public async Task SeedAsync(Action<GuardiaoDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }
}

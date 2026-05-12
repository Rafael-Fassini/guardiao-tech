using System.Net;
using System.Net.Http.Json;
using System.Linq;
using System.Text.Json;
using Guardiao.Api.Contracts;
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

    public InstitutionsControllerIntegrationTests(GuardiaoApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostInstitutions_ShouldCreateInstitution()
    {
        var request = new CreateInstitutionRequest
        {
            Name = "School A",
            Address = "Main Avenue"
        };

        var response = await _client.PostAsJsonAsync("/api/institutions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }
}

public class GuardiaoApiFactory : WebApplicationFactory<Program>
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
                ["VictimRegistry:InitialLookbackMinutes"] = "5"
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

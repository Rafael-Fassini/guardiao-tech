using System.Net;
using System.Net.Http.Json;
using System.Linq;
using Guardiao.Api.Contracts;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        });
    }
}

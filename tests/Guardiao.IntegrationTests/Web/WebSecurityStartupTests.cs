using Guardiao.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Guardiao.IntegrationTests.Web;

[Trait("Category", "Security")]
public class WebSecurityStartupTests
{
    [Fact]
    public void CreateClient_ShouldFail_WhenDemoLoginIsEnabledOutsideDevelopment()
    {
        using var factory = new InvalidWebSecurityFactory();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("EnableOperationsDemoLogin", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLogin_ShouldEmitCspCompatibleWithBlazorServer()
    {
        using var factory = new GuardiaoWebFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/login");

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Content-Security-Policy", out var values));

        var csp = values.Single();
        Assert.Contains("connect-src 'self' ws: wss:", csp, StringComparison.Ordinal);
        Assert.Contains("style-src 'self' 'unsafe-inline'", csp, StringComparison.Ordinal);
    }
}

internal sealed class InvalidWebSecurityFactory : WebApplicationFactory<WebEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PanelApi:BaseUrl"] = "https://operations-api.test",
                ["PanelApi:SharedSecret"] = "production-panel-secret",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=guardiao_tests;Username=guardiao;Password=guardiao",
                ["WebSecurity:EnableOperationsDemoLogin"] = "true"
            });
        });
    }
}

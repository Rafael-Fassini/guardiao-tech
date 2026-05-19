using Xunit;

namespace Guardiao.UnitTests.Operations;

public class DeploymentConfigValidationTests
{
    [Fact]
    public void DeploymentArtifacts_ShouldExist()
    {
        Assert.True(File.Exists(ToRoot(".env.example")));
        Assert.True(File.Exists(ToRoot("docker-compose.yml")));
        Assert.True(File.Exists(ToRoot("src/Guardiao.Api/Dockerfile")));
        Assert.True(File.Exists(ToRoot("src/Guardiao.Web/Dockerfile")));
        Assert.True(File.Exists(ToRoot("src/Guardiao.Worker.Edge/Dockerfile")));
        Assert.True(File.Exists(ToRoot("scripts/post-deploy-smoke.sh")));
        Assert.True(File.Exists(ToRoot("scripts/validate-deployment-config.sh")));
    }

    [Fact]
    public void EnvTemplate_ShouldDisableSensitivePilotFlags()
    {
        var envFile = File.ReadAllText(ToRoot(".env.example"));

        Assert.Contains("API_ENABLE_DEBUG_HEADER_AUTHENTICATION=false", envFile, StringComparison.Ordinal);
        Assert.Contains("WEB_ENABLE_OPERATIONS_DEMO_LOGIN=false", envFile, StringComparison.Ordinal);
        Assert.Contains("VICTIM_REGISTRY_WEBHOOK_SECRET=", envFile, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeFile_ShouldDeclarePilotStartupOrdering()
    {
        var compose = File.ReadAllText(ToRoot("docker-compose.yml"));

        Assert.Contains("depends_on:", compose, StringComparison.Ordinal);
        Assert.Contains("condition: service_healthy", compose, StringComparison.Ordinal);
        Assert.Contains("healthcheck:", compose, StringComparison.Ordinal);
        Assert.Contains("api:", compose, StringComparison.Ordinal);
        Assert.Contains("web:", compose, StringComparison.Ordinal);
        Assert.Contains("worker:", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionTemplates_ShouldExistForAllHosts()
    {
        Assert.True(File.Exists(ToRoot("src/Guardiao.Api/appsettings.Production.template.json")));
        Assert.True(File.Exists(ToRoot("src/Guardiao.Web/appsettings.Production.template.json")));
        Assert.True(File.Exists(ToRoot("src/Guardiao.Worker.Edge/appsettings.Production.template.json")));
    }

    private static string ToRoot(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath));
    }
}

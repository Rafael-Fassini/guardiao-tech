using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class ApiSecurityOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldFail_WhenProductionSecretsAreWeak()
    {
        var validator = new ApiSecurityOptionsValidator(new FakeHostEnvironment("Production"));

        var result = validator.Validate(null, new ApiSecurityOptions
        {
            PanelSharedSecret = "short",
            WorkerSharedSecret = "tiny",
            MaxApiRequestBodyBytes = 1024 * 1024
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("PanelSharedSecret", StringComparison.Ordinal));
        Assert.Contains(result.Failures, x => x.Contains("WorkerSharedSecret", StringComparison.Ordinal));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Guardiao";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }
}

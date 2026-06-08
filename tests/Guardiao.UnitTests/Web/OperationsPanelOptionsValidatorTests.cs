using Guardiao.Web.Services;
using Xunit;

namespace Guardiao.UnitTests.Web;

public class OperationsPanelOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldFail_WhenSharedSecretIsTooShort()
    {
        var validator = new OperationsPanelOptionsValidator();

        var result = validator.Validate(null, new OperationsPanelOptions
        {
            BaseUrl = "https://operations-api.test",
            SharedSecret = "short"
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("SharedSecret", StringComparison.Ordinal));
    }
}

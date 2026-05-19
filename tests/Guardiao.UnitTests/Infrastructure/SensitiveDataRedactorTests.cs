using Guardiao.Infrastructure.Security;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class SensitiveDataRedactorTests
{
    [Fact]
    public void RedactIdentifier_ShouldMaskMiddleSegment()
    {
        var redactor = new SensitiveDataRedactor();

        var value = redactor.RedactIdentifier("case-123456");

        Assert.Equal("cas***456", value);
    }

    [Fact]
    public void RedactSecret_ShouldNeverExposeOriginalValue()
    {
        var redactor = new SensitiveDataRedactor();

        var value = redactor.RedactSecret("super-secret-token");

        Assert.Equal("[redacted]", value);
        Assert.DoesNotContain("super-secret-token", value, StringComparison.Ordinal);
    }
}

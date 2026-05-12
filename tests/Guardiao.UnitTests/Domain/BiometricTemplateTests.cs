using Guardiao.Domain.Entities;
using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class BiometricTemplateTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenProjectionIsMissing()
    {
        Assert.Throws<InvariantViolationException>(() =>
            new BiometricTemplate(
                Guid.Empty,
                new ExternalPersonId("person-1"),
                [0.1f, 0.2f],
                RetentionMode.CaseBound,
                false));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSubjectIsBystander()
    {
        Assert.Throws<InvariantViolationException>(() =>
            new BiometricTemplate(
                Guid.NewGuid(),
                new ExternalPersonId("person-1"),
                [0.1f, 0.2f],
                RetentionMode.CaseBound,
                true));
    }
}

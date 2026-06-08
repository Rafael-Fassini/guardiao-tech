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
                false,
                "upload",
                "face.png",
                "image/png",
                "bucket/face.png"));
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
                true,
                "upload",
                "face.png",
                "image/png",
                "bucket/face.png"));
    }

    [Fact]
    public void Deactivate_ShouldMarkTemplateInactive()
    {
        var template = new BiometricTemplate(
            Guid.NewGuid(),
            new ExternalPersonId("person-1"),
            [0.1f, 0.2f],
            RetentionMode.CaseBound,
            false,
            "upload",
            "face.png",
            "image/png",
            "bucket/face.png");

        template.Deactivate();

        Assert.False(template.IsActive);
        Assert.NotNull(template.DeactivatedAtUtc);
    }
}

using Guardiao.Domain.Entities;
using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class ProtectedCaseTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenVersionIsNotPositive()
    {
        Assert.Throws<InvariantViolationException>(() =>
            new ProtectedCase(
                new ExternalCaseId("case-1"),
                0,
                Guid.NewGuid(),
                Guid.NewGuid(),
                MonitoringStatus.Enabled,
                ConsentStatus.Granted));
    }

    [Fact]
    public void SynchronizeFromSource_ShouldThrow_WhenIncomingVersionIsStale()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-1"),
            3,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        Assert.Throws<ForbiddenStateTransitionException>(() =>
            protectedCase.SynchronizeFromSource(
                2,
                MonitoringStatus.Enabled,
                ConsentStatus.Granted,
                DateTime.UtcNow));
    }
}

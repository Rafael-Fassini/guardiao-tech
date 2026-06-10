using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
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

    [Fact]
    public void Constructor_ShouldDefaultSubjectRoleToProtectedWoman()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-default"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        Assert.Equal(MonitoredSubjectRole.ProtectedWoman, protectedCase.SubjectRole);
    }

    [Fact]
    public void Reclassify_ShouldReturnTrueAndUpdateRole_WhenRoleChanges()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-change"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        var changed = protectedCase.Reclassify(MonitoredSubjectRole.Aggressor);

        Assert.True(changed);
        Assert.Equal(MonitoredSubjectRole.Aggressor, protectedCase.SubjectRole);
    }

    [Fact]
    public void Reclassify_ShouldReturnFalse_WhenRoleDoesNotChange()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-role-same"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        var changed = protectedCase.Reclassify(MonitoredSubjectRole.ProtectedWoman);

        Assert.False(changed);
        Assert.Equal(MonitoredSubjectRole.ProtectedWoman, protectedCase.SubjectRole);
    }
}

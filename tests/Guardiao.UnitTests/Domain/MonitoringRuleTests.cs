using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class MonitoringRuleTests
{
    [Fact]
    public void AppliesTo_ShouldReturnTrue_WhenScopeAndWindowMatch()
    {
        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        var rule = new MonitoringRule(
            Guid.NewGuid(),
            scope,
            new TimeWindow(new TimeOnly(8, 0), new TimeOnly(18, 0)),
            true);

        var applies = rule.AppliesTo(scope, new TimeOnly(12, 0));

        Assert.True(applies);
    }

    [Fact]
    public void AppliesTo_ShouldReturnFalse_WhenRuleIsDisabled()
    {
        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        var rule = new MonitoringRule(
            Guid.NewGuid(),
            scope,
            new TimeWindow(new TimeOnly(8, 0), new TimeOnly(18, 0)),
            false);

        var applies = rule.AppliesTo(scope, new TimeOnly(12, 0));

        Assert.False(applies);
    }
}

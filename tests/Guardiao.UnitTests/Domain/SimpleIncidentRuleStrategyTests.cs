using Guardiao.Domain.Entities;
using Guardiao.Domain.Strategies;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class SimpleIncidentRuleStrategyTests
{
    [Fact]
    public void ShouldCreateIncident_ShouldReturnTrue_WhenDetectionMatchesCase()
    {
        var strategy = new SimpleIncidentRuleStrategy();
        var protectedId = Guid.NewGuid();
        var restrictedId = Guid.NewGuid();
        var protectiveCase = new ProtectiveCase(Guid.NewGuid(), protectedId, restrictedId, "Open");
        var detectionEvent = new DetectionEvent(Guid.NewGuid(), protectedId, restrictedId, DateTime.UtcNow);

        var shouldCreate = strategy.ShouldCreateIncident(detectionEvent, protectiveCase);

        Assert.True(shouldCreate);
    }

    [Fact]
    public void ShouldCreateIncident_ShouldReturnFalse_WhenDetectionDoesNotMatchCase()
    {
        var strategy = new SimpleIncidentRuleStrategy();
        var protectiveCase = new ProtectiveCase(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Open");
        var detectionEvent = new DetectionEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

        var shouldCreate = strategy.ShouldCreateIncident(detectionEvent, protectiveCase);

        Assert.False(shouldCreate);
    }
}

using Guardiao.Domain.Entities;
using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class BiometricCandidateEventTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenCameraScopeIsInvalid()
    {
        Assert.Throws<InvariantViolationException>(() => new CameraScope(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<InvariantViolationException>(() => new CameraScope(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void Constructor_ShouldCreateEvent_WhenScopeAndScoreAreValid()
    {
        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        var candidateEvent = new BiometricCandidateEvent(
            Guid.NewGuid(),
            scope,
            new MatchScore(0.91),
            DateTime.UtcNow);

        Assert.Equal(scope, candidateEvent.CameraScope);
    }
}

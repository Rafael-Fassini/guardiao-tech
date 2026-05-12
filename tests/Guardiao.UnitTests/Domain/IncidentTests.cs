using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.Exceptions;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class IncidentTests
{
    [Fact]
    public void Escalate_ShouldThrow_WhenIncidentWasNotConfirmedByHuman()
    {
        var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());

        Assert.Throws<ForbiddenStateTransitionException>(() => incident.Escalate());
    }

    [Fact]
    public void Escalate_ShouldSucceed_AfterConfirmation()
    {
        var incident = new Incident(Guid.NewGuid(), Guid.NewGuid());
        incident.ConfirmReview("validated by operator");

        incident.Escalate();

        Assert.Equal(IncidentStatus.Escalated, incident.Status);
    }
}

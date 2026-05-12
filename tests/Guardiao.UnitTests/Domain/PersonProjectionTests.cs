using Guardiao.Domain.Entities;
using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Domain;

public class PersonProjectionTests
{
    [Fact]
    public void EditSourceOfTruthFields_ShouldAlwaysThrow()
    {
        var projection = new PersonProjection(
            new ExternalPersonId("person-1"),
            Guid.NewGuid(),
            "Maria da Silva",
            false,
            DateTime.UtcNow);

        Assert.Throws<ForbiddenStateTransitionException>(() => projection.EditSourceOfTruthFields());
    }
}

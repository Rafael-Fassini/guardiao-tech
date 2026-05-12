namespace Guardiao.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public sealed class InvariantViolationException : DomainException
{
    public InvariantViolationException(string message) : base(message)
    {
    }
}

public sealed class ForbiddenStateTransitionException : DomainException
{
    public ForbiddenStateTransitionException(string message) : base(message)
    {
    }
}

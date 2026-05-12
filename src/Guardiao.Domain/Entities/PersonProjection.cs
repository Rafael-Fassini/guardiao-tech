using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class PersonProjection
{
    private PersonProjection()
    {
    }

    public PersonProjection(
        ExternalPersonId externalPersonId,
        Guid protectedCaseId,
        string fullName,
        bool isBystander,
        DateTime sourceUpdatedAtUtc)
    {
        if (protectedCaseId == Guid.Empty)
        {
            throw new InvariantViolationException("Person projection must belong to a protected case.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvariantViolationException("Person projection full name is required.");
        }

        Id = Guid.NewGuid();
        ExternalPersonId = externalPersonId;
        ProtectedCaseId = protectedCaseId;
        FullName = fullName.Trim();
        IsBystander = isBystander;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
    }

    public Guid Id { get; private set; }
    public ExternalPersonId ExternalPersonId { get; private set; }
    public Guid ProtectedCaseId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public bool IsBystander { get; private set; }
    public DateTime SourceUpdatedAtUtc { get; private set; }

    public void RefreshFromSource(string fullName, bool isBystander, DateTime sourceUpdatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvariantViolationException("Person projection full name is required.");
        }

        FullName = fullName.Trim();
        IsBystander = isBystander;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
    }

    public void EditSourceOfTruthFields()
    {
        throw new ForbiddenStateTransitionException("Internal edits to source-of-truth victim fields are forbidden.");
    }
}

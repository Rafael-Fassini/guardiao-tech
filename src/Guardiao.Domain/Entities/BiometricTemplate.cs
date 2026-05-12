using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class BiometricTemplate
{
    private BiometricTemplate()
    {
    }

    public BiometricTemplate(
        Guid personProjectionId,
        ExternalPersonId externalPersonId,
        IReadOnlyCollection<float> embedding,
        RetentionMode retentionMode,
        bool isBystander)
    {
        if (personProjectionId == Guid.Empty)
        {
            throw new InvariantViolationException("Biometric template requires a person projection.");
        }

        if (embedding.Count == 0)
        {
            throw new InvariantViolationException("Biometric template embedding cannot be empty.");
        }

        if (isBystander)
        {
            throw new InvariantViolationException("Bystander data must not be persisted as biometric templates.");
        }

        Id = Guid.NewGuid();
        PersonProjectionId = personProjectionId;
        ExternalPersonId = externalPersonId;
        Embedding = [.. embedding];
        RetentionMode = retentionMode;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PersonProjectionId { get; private set; }
    public ExternalPersonId ExternalPersonId { get; private set; }
    public IReadOnlyCollection<float> Embedding { get; private set; } = Array.Empty<float>();
    public RetentionMode RetentionMode { get; private set; }
    public DateTime CreatedAt { get; private set; }
}

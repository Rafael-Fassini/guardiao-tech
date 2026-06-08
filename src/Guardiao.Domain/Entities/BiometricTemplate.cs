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
        bool isBystander,
        string source,
        string displayName,
        string contentType,
        string storagePath)
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

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvariantViolationException("Biometric template source is required.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvariantViolationException("Biometric template display name is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvariantViolationException("Biometric template content type is required.");
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new InvariantViolationException("Biometric template storage path is required.");
        }

        Id = Guid.NewGuid();
        PersonProjectionId = personProjectionId;
        ExternalPersonId = externalPersonId;
        Embedding = [.. embedding];
        RetentionMode = retentionMode;
        Source = source.Trim();
        DisplayName = displayName.Trim();
        ContentType = contentType.Trim();
        StoragePath = storagePath.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid PersonProjectionId { get; private set; }
    public ExternalPersonId ExternalPersonId { get; private set; }
    public IReadOnlyCollection<float> Embedding { get; private set; } = Array.Empty<float>();
    public RetentionMode RetentionMode { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public string StoragePath { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeactivatedAtUtc { get; private set; }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        DeactivatedAtUtc = DateTime.UtcNow;
    }
}

using Guardiao.Domain.Enums;
using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class EvidenceArtifact
{
    private EvidenceArtifact()
    {
    }

    public EvidenceArtifact(
        Guid incidentId,
        Guid? candidateEventId,
        EvidenceArtifactType artifactType,
        string storagePath,
        string contentType,
        RetentionMode retentionMode)
    {
        if (incidentId == Guid.Empty)
        {
            throw new InvariantViolationException("Evidence artifact must belong to an incident.");
        }

        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new InvariantViolationException("Evidence artifact storage path is required.");
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new InvariantViolationException("Evidence artifact content type is required.");
        }

        Id = Guid.NewGuid();
        IncidentId = incidentId;
        CandidateEventId = candidateEventId;
        ArtifactType = artifactType;
        StoragePath = storagePath.Trim();
        ContentType = contentType.Trim();
        RetentionMode = retentionMode;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid IncidentId { get; private set; }
    public Guid? CandidateEventId { get; private set; }
    public EvidenceArtifactType ArtifactType { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public RetentionMode RetentionMode { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
}

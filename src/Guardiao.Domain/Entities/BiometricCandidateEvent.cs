using Guardiao.Domain.Exceptions;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Domain.Entities;

public class BiometricCandidateEvent
{
    private BiometricCandidateEvent()
    {
    }

    public BiometricCandidateEvent(
        Guid protectedCaseId,
        CameraScope cameraScope,
        MatchScore matchScore,
        DateTime occurredAtUtc)
        : this(Guid.NewGuid(), protectedCaseId, cameraScope, matchScore, occurredAtUtc)
    {
    }

    public BiometricCandidateEvent(
        Guid id,
        Guid protectedCaseId,
        CameraScope cameraScope,
        MatchScore matchScore,
        DateTime occurredAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new InvariantViolationException("Candidate event id is required.");
        }

        if (protectedCaseId == Guid.Empty)
        {
            throw new InvariantViolationException("Candidate event must reference a protected case.");
        }

        Id = id;
        ProtectedCaseId = protectedCaseId;
        CameraScope = cameraScope;
        MatchScore = matchScore;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProtectedCaseId { get; private set; }
    public CameraScope CameraScope { get; private set; }
    public MatchScore MatchScore { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}

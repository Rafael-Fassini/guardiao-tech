using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public sealed class CandidateEventIngestionRequest
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid ProtectedCaseId { get; set; }

    [Required]
    public Guid SiteId { get; set; }

    [Required]
    public Guid CameraId { get; set; }

    [Range(0d, 1d)]
    public double MatchScore { get; set; }

    [Required]
    public DateTime OccurredAtUtc { get; set; }

    public List<CandidateEventEvidenceRequest> Evidences { get; set; } = [];
}

public sealed class CandidateEventEvidenceRequest
{
    [Required]
    public string ArtifactType { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public byte[] Content { get; set; } = [];
}

public sealed record CandidateEventIngestionResponse(
    Guid CandidateEventId,
    bool WasDuplicate,
    bool CreatesIncident,
    string DecisionReasonCode,
    Guid? IncidentId);

using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public sealed record OperationsSummaryResponse(
    int IncidentCount,
    int CaseCount,
    int CameraCount,
    int AuditEntryCount,
    IReadOnlyCollection<RecentIncidentResponse> RecentIncidents,
    IReadOnlyCollection<AuditEntryResponse> RecentAuditEntries,
    IReadOnlyCollection<CameraOperationalViewResponse> CameraViews);

public sealed record CameraOperationalViewResponse(
    Guid CameraId,
    Guid SiteId,
    string CameraName,
    string SiteName,
    bool IsEnabled,
    string StreamEndpoint,
    DateTime? LastDetectionAtUtc,
    EvidencePreviewResponse? LatestSnapshot,
    IReadOnlyCollection<DetectedSubjectResponse> RecentProtectedWomen,
    AggressorPresenceAlertResponse? ActiveAlert);

public sealed record EvidencePreviewResponse(
    Guid IncidentId,
    Guid EvidenceId,
    string ContentType,
    DateTime CapturedAtUtc);

public sealed record DetectedSubjectResponse(
    Guid ProtectedCaseId,
    Guid PersonProjectionId,
    string FullName,
    string SubjectRole,
    bool IsBystander,
    string IncidentStatus,
    double MatchScore,
    DateTime DetectedAtUtc,
    EvidencePreviewResponse? Snapshot);

public sealed record AggressorPresenceAlertResponse(
    Guid ProtectedCaseId,
    string FullName,
    double MatchScore,
    DateTime DetectedAtUtc,
    IReadOnlyCollection<string> NearbyProtectedWomen,
    EvidencePreviewResponse? Snapshot);

public sealed record RecentIncidentResponse(
    Guid Id,
    Guid ProtectedCaseId,
    string Status,
    DateTime CreatedAtUtc);

public sealed record IncidentListItemResponse(
    Guid Id,
    Guid ProtectedCaseId,
    Guid CandidateEventId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? EscalatedAtUtc);

public sealed record IncidentDetailResponse(
    Guid Id,
    Guid ProtectedCaseId,
    Guid CandidateEventId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? EscalatedAtUtc,
    string? ReviewNotes);

public sealed record IncidentNotificationResponse(
    Guid Id,
    Guid IncidentId,
    string EventType,
    string Channel,
    string DeliveryStatus,
    int AttemptCount,
    bool HasEvidence,
    string Details,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record IncidentEvidenceResponse(
    Guid Id,
    Guid IncidentId,
    Guid? CandidateEventId,
    string ArtifactType,
    string ContentType,
    DateTime CreatedAtUtc);

public sealed record ProtectedCaseListItemResponse(
    Guid Id,
    string ExternalCaseId,
    string SubjectRole,
    long Version,
    string MonitoringStatus,
    string ConsentStatus,
    DateTime LastSynchronizedAt,
    string LastSyncStatus);

public sealed record ProtectedCaseDetailResponse(
    Guid Id,
    string ExternalCaseId,
    string SubjectRole,
    long Version,
    string MonitoringStatus,
    string ConsentStatus,
    Guid PersonProjectionId,
    DateTime CreatedAt,
    DateTime LastSynchronizedAt,
    string LastSyncStatus,
    string? LastSyncFailureReason);

public sealed record BiometricTemplateResponse(
    Guid Id,
    Guid PersonProjectionId,
    string ExternalPersonId,
    string Source,
    string DisplayName,
    string ContentType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? DeactivatedAtUtc);

public sealed record BiometricTemplateUploadResponse(
    Guid Id,
    Guid PersonProjectionId,
    string ExternalPersonId,
    string DisplayName,
    string ContentType,
    DateTime CreatedAt);

public sealed record BiometricGalleryEntryResponse(
    Guid ProtectedCaseId,
    Guid SiteId,
    Guid PersonProjectionId,
    string ExternalPersonId,
    bool IsBystander,
    IReadOnlyCollection<float> Embedding);

public sealed record MonitoringRuleResponse(
    Guid Id,
    Guid SiteId,
    Guid CameraId,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    bool IsEnabled);

public sealed record SiteResponse(
    Guid Id,
    Guid InstitutionId,
    string Name,
    string AddressLine);

public sealed record CameraResponse(
    Guid Id,
    Guid SiteId,
    string Name,
    string StreamEndpoint,
    bool IsEnabled);

public sealed record AuditEntryResponse(
    Guid Id,
    string ActorType,
    string Action,
    string EntityName,
    string EntityId,
    string Details,
    DateTime CreatedAtUtc);

public sealed class UpdateCameraStateRequest
{
    [Required]
    public bool IsEnabled { get; set; }
}

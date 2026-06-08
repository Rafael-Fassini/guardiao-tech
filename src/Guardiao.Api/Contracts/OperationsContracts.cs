using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public sealed record OperationsSummaryResponse(
    int IncidentCount,
    int CaseCount,
    int CameraCount,
    int AuditEntryCount,
    IReadOnlyCollection<RecentIncidentResponse> RecentIncidents,
    IReadOnlyCollection<AuditEntryResponse> RecentAuditEntries);

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
    long Version,
    string MonitoringStatus,
    string ConsentStatus,
    DateTime LastSynchronizedAt,
    string LastSyncStatus);

public sealed record ProtectedCaseDetailResponse(
    Guid Id,
    string ExternalCaseId,
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

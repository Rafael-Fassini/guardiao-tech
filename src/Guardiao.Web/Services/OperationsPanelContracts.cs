namespace Guardiao.Web.Services;

public sealed record OperationsSummaryModel(
    int IncidentCount,
    int CaseCount,
    int CameraCount,
    int AuditEntryCount,
    IReadOnlyCollection<RecentIncidentModel> RecentIncidents,
    IReadOnlyCollection<AuditEntryModel> RecentAuditEntries,
    IReadOnlyCollection<CameraOperationalViewModel> CameraViews);

public sealed record CameraOperationalViewModel(
    Guid CameraId,
    Guid SiteId,
    string CameraName,
    string SiteName,
    bool IsEnabled,
    string StreamEndpoint,
    DateTime? LastDetectionAtUtc,
    EvidencePreviewModel? LatestSnapshot,
    IReadOnlyCollection<DetectedSubjectModel> RecentProtectedWomen,
    AggressorPresenceAlertModel? ActiveAlert);

public sealed record EvidencePreviewModel(
    Guid IncidentId,
    Guid EvidenceId,
    string ContentType,
    DateTime CapturedAtUtc);

public sealed record CameraLivePreviewModel(
    string DataUrl,
    DateTime? CapturedAtUtc);

public sealed record DetectedSubjectModel(
    Guid ProtectedCaseId,
    Guid PersonProjectionId,
    string FullName,
    string SubjectRole,
    bool IsBystander,
    string IncidentStatus,
    double MatchScore,
    DateTime DetectedAtUtc,
    EvidencePreviewModel? Snapshot);

public sealed record AggressorPresenceAlertModel(
    Guid ProtectedCaseId,
    string FullName,
    double MatchScore,
    DateTime DetectedAtUtc,
    IReadOnlyCollection<string> NearbyProtectedWomen,
    EvidencePreviewModel? Snapshot);

public sealed record RecentIncidentModel(
    Guid Id,
    Guid ProtectedCaseId,
    string Status,
    DateTime CreatedAtUtc);

public sealed record IncidentListItemModel(
    Guid Id,
    Guid ProtectedCaseId,
    Guid CandidateEventId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? EscalatedAtUtc);

public sealed record IncidentDetailModel(
    Guid Id,
    Guid ProtectedCaseId,
    Guid CandidateEventId,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateTime? EscalatedAtUtc,
    string? ReviewNotes);

public sealed record IncidentNotificationModel(
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

public sealed record IncidentEvidenceModel(
    Guid Id,
    Guid IncidentId,
    Guid? CandidateEventId,
    string ArtifactType,
    string ContentType,
    DateTime CreatedAtUtc);

public sealed record ProtectedCaseListItemModel(
    Guid Id,
    string ExternalCaseId,
    string SubjectRole,
    long Version,
    string MonitoringStatus,
    string ConsentStatus,
    DateTime LastSynchronizedAt,
    string LastSyncStatus);

public sealed record ProtectedCaseDetailModel(
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

public sealed record BiometricTemplateModel(
    Guid Id,
    Guid PersonProjectionId,
    string ExternalPersonId,
    string Source,
    string DisplayName,
    string ContentType,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? DeactivatedAtUtc);

public sealed record BiometricTemplateUploadModel(
    Guid Id,
    Guid PersonProjectionId,
    string ExternalPersonId,
    string DisplayName,
    string ContentType,
    DateTime CreatedAt);

public sealed record MonitoringRuleModel(
    Guid Id,
    Guid SiteId,
    Guid CameraId,
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    bool IsEnabled);

public sealed record SiteModel(
    Guid Id,
    Guid InstitutionId,
    string Name,
    string AddressLine);

public sealed record CameraModel(
    Guid Id,
    Guid SiteId,
    string Name,
    string StreamEndpoint,
    bool IsEnabled);

public sealed record AuditEntryModel(
    Guid Id,
    string ActorType,
    string Action,
    string EntityName,
    string EntityId,
    string Details,
    DateTime CreatedAtUtc);

internal sealed class IncidentReviewRequest
{
    public string ReviewNotes { get; set; } = string.Empty;
}

internal sealed class UpdateMonitoringRuleRequest
{
    public Guid SiteId { get; set; }
    public Guid CameraId { get; set; }
    public TimeOnly StartsAt { get; set; }
    public TimeOnly EndsAt { get; set; }
    public bool IsEnabled { get; set; }
}

internal sealed class UpdateCameraStateRequest
{
    public bool IsEnabled { get; set; }
}

internal sealed class UpdateProtectedCaseSubjectRoleRequest
{
    public string SubjectRole { get; set; } = string.Empty;
}

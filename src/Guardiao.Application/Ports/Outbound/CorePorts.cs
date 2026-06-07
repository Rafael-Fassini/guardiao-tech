using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Application.Ports.Outbound;

public interface IVictimRegistryPort
{
    Task<VictimRegistryCaseSnapshot?> GetCaseAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<VictimRegistryCaseSnapshot>> GetCasesUpdatedSinceAsync(DateTime updatedSinceUtc, int page, int pageSize, CancellationToken cancellationToken = default);
}

public interface IVictimRegistryMediaPort
{
    Task<IReadOnlyCollection<VictimRegistryMediaItem>> ListMediaAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default);
    Task<Stream> DownloadMediaAsync(ExternalCaseId externalCaseId, string mediaId, CancellationToken cancellationToken = default);
}

public interface IWebhookSignatureVerifier
{
    bool IsValid(string payload, string signature, DateTimeOffset timestamp);
}

public interface ICaseProjectionRepository
{
    Task<ProtectedCase?> GetByExternalIdAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default);
    Task<ProtectedCase?> GetByIdAsync(Guid protectedCaseId, CancellationToken cancellationToken = default);
    Task UpsertAsync(ProtectedCase protectedCase, PersonProjection personProjection, CancellationToken cancellationToken = default);
    Task MarkSyncFailureAsync(ExternalCaseId externalCaseId, string failureReason, DateTime occurredAtUtc, CancellationToken cancellationToken = default);
}

public interface IIncidentRepository
{
    Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default);
    Task<Incident?> GetByCandidateEventIdAsync(Guid candidateEventId, CancellationToken cancellationToken = default);
    Task<Incident?> FindLatestActiveByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default);
    Task AddAsync(Incident incident, CancellationToken cancellationToken = default);
    Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default);
}

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}

public interface IMonitoringRuleRepository
{
    Task<IReadOnlyCollection<MonitoringRule>> ListByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default);
}

public interface ICorrelationDecisionRepository
{
    Task AddAsync(CorrelationDecision decision, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CorrelationDecision>> ListByCandidateEventAsync(Guid candidateEventId, CancellationToken cancellationToken = default);
}

public interface ICandidateEventRepository
{
    Task<BiometricCandidateEvent?> GetByIdAsync(Guid candidateEventId, CancellationToken cancellationToken = default);
    Task AddAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default);
}

public interface IBiometricTemplateRepository
{
    Task AddAsync(BiometricTemplate biometricTemplate, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<BiometricTemplate>> ListByPersonAsync(Guid personProjectionId, CancellationToken cancellationToken = default);
}

public interface IEvidenceStoragePort
{
    Task<string> StoreAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
}

public interface ICameraCapturePort
{
    Task<Stream> CaptureFrameAsync(Camera camera, CancellationToken cancellationToken = default);
}

public interface IFaceDetectorPort
{
    Task<IReadOnlyCollection<DetectedFace>> DetectAsync(Stream frame, CancellationToken cancellationToken = default);
}

public interface IFaceTrackerPort
{
    Task<IReadOnlyCollection<TrackedFace>> TrackAsync(IReadOnlyCollection<DetectedFace> faces, CancellationToken cancellationToken = default);
}

public interface IFaceEmbedderPort
{
    Task<IReadOnlyCollection<float>> CreateEmbeddingAsync(TrackedFace face, CancellationToken cancellationToken = default);
}

public interface IFaceMatcherPort
{
    Task<MatchScore> MatchAsync(IReadOnlyCollection<float> embedding, Guid protectedCaseId, CancellationToken cancellationToken = default);
}

public interface ICandidateEventPublisher
{
    Task PublishAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default);
}

public interface INotificationPort
{
    Task NotifyIncidentCreatedAsync(Incident incident, CancellationToken cancellationToken = default);
}

public interface IMetricsPort
{
    void IncrementCounter(string name, params (string Key, string Value)[] tags);
    void RecordLatency(string name, TimeSpan elapsed, params (string Key, string Value)[] tags);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IWebhookDeliveryRepository
{
    Task<bool> TryRegisterAsync(Guid deliveryId, string eventType, DateTimeOffset receivedAtUtc, CancellationToken cancellationToken = default);
}

public interface ISyncCursorRepository
{
    Task<DateTime?> GetLastCursorAsync(string cursorName, CancellationToken cancellationToken = default);
    Task SaveLastCursorAsync(string cursorName, DateTime cursorUtc, CancellationToken cancellationToken = default);
}

public interface IVictimRegistrySyncQueue
{
    ValueTask EnqueueAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default);
    ValueTask<ExternalCaseId> DequeueAsync(CancellationToken cancellationToken);
}

public interface IShortLivedStatePort
{
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record VictimRegistryCaseSnapshot(
    ExternalCaseId ExternalCaseId,
    ExternalPersonId ExternalPersonId,
    long Version,
    string FullName,
    MonitoringStatus MonitoringStatus,
    ConsentStatus ConsentStatus,
    bool IsBystander,
    DateTime UpdatedAtUtc);

public sealed record VictimRegistryMediaItem(string MediaId, string ContentType, DateTime CreatedAtUtc);

public sealed record DetectedFace(Guid DetectionId, byte[] CropBytes);

public sealed record TrackedFace(Guid TrackingId, byte[] CropBytes);

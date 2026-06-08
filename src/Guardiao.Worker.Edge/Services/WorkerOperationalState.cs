using System.Collections.Concurrent;
using Guardiao.Worker.Edge.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public sealed class WorkerOperationalState
{
    private readonly IReadOnlyCollection<Guid> _enabledCameraIds;
    private readonly int _expectedScopeCount;
    private readonly ConcurrentDictionary<Guid, DateTime> _cameraLastSuccessUtc = new();
    private readonly ConcurrentDictionary<Guid, DateTime> _cameraLastFailureUtc = new();
    private readonly ConcurrentDictionary<Guid, int> _cameraFailureCounts = new();

    private int _lastGallerySuccessfulScopes;
    private DateTime? _lastGalleryRefreshSuccessUtc;
    private DateTime? _lastGalleryRefreshFailureUtc;

    public WorkerOperationalState(IOptions<EdgeWorkerOptions> options)
    {
        var configured = options.Value.Cameras.Where(x => x.Enabled).ToArray();
        _enabledCameraIds = configured.Select(x => x.CameraId).Distinct().ToArray();
        _expectedScopeCount = configured.Select(x => (x.ProtectedCaseId, x.SiteId)).Distinct().Count();
    }

    public void RecordCameraSuccess(Guid cameraId, DateTime occurredAtUtc)
    {
        _cameraLastSuccessUtc[cameraId] = occurredAtUtc;
    }

    public void RecordCameraFailure(Guid cameraId, DateTime occurredAtUtc)
    {
        _cameraLastFailureUtc[cameraId] = occurredAtUtc;
        _cameraFailureCounts.AddOrUpdate(cameraId, 1, (_, current) => current + 1);
    }

    public void RecordGalleryRefresh(int successfulScopes, DateTime occurredAtUtc)
    {
        _lastGallerySuccessfulScopes = successfulScopes;
        _lastGalleryRefreshSuccessUtc = occurredAtUtc;
    }

    public void RecordGalleryRefreshFailure(DateTime occurredAtUtc)
    {
        _lastGalleryRefreshFailureUtc = occurredAtUtc;
    }

    public WorkerReadinessSnapshot Snapshot(DateTime nowUtc, int galleryRefreshIntervalSeconds)
    {
        var freshnessWindow = TimeSpan.FromSeconds(Math.Max(60, galleryRefreshIntervalSeconds * 2));
        var staleCameras = _enabledCameraIds
            .Where(cameraId => !_cameraLastSuccessUtc.TryGetValue(cameraId, out var lastSuccess) || nowUtc - lastSuccess > freshnessWindow)
            .ToArray();

        var hasFreshGallery = _lastGalleryRefreshSuccessUtc is DateTime gallerySuccessUtc &&
                              nowUtc - gallerySuccessUtc <= freshnessWindow &&
                              _lastGallerySuccessfulScopes >= _expectedScopeCount;

        var isReady = _enabledCameraIds.Count > 0 && hasFreshGallery && staleCameras.Length == 0;

        return new WorkerReadinessSnapshot(
            isReady,
            _enabledCameraIds.Count,
            _expectedScopeCount,
            _lastGallerySuccessfulScopes,
            _lastGalleryRefreshSuccessUtc,
            _lastGalleryRefreshFailureUtc,
            staleCameras,
            _cameraFailureCounts.ToDictionary(x => x.Key.ToString(), x => x.Value));
    }
}

public sealed record WorkerReadinessSnapshot(
    bool IsReady,
    int EnabledCameraCount,
    int ExpectedScopeCount,
    int CachedScopeCount,
    DateTime? LastGalleryRefreshSuccessUtc,
    DateTime? LastGalleryRefreshFailureUtc,
    IReadOnlyCollection<Guid> StaleCameraIds,
    IReadOnlyDictionary<string, int> CameraFailureCounts);

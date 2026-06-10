using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;

namespace Guardiao.Worker.Edge.Services;

public sealed class CameraPipelineSession
{
    private readonly ICameraCapturePort _capturePort;
    private readonly IFaceDetectorPort _detectorPort;
    private readonly IFaceTrackerPort _trackerPort;
    private readonly IFaceEmbedderPort _embedderPort;
    private readonly IFaceMatcherPort _matcherPort;
    private readonly ICandidateEventPublisher _publisher;
    private readonly IRestrictedGalleryProvider _galleryProvider;
    private readonly CandidateEventEvidenceFactory _evidenceFactory;
    private readonly BoundedCameraFrameQueue _queue;
    private readonly EdgeMetricsCollector _metrics;
    private readonly IClock _clock;
    private readonly LatestCameraFrameStore _latestCameraFrameStore;
    private readonly FrameSamplerFactory _samplerFactory;
    private readonly Dictionary<Guid, FrameSampler> _samplers = [];
    private readonly Dictionary<Guid, long> _sequences = [];

    public CameraPipelineSession(
        ICameraCapturePort capturePort,
        IFaceDetectorPort detectorPort,
        IFaceTrackerPort trackerPort,
        IFaceEmbedderPort embedderPort,
        IFaceMatcherPort matcherPort,
        ICandidateEventPublisher publisher,
        IRestrictedGalleryProvider galleryProvider,
        CandidateEventEvidenceFactory evidenceFactory,
        BoundedCameraFrameQueue queue,
        EdgeMetricsCollector metrics,
        IClock clock,
        LatestCameraFrameStore latestCameraFrameStore,
        FrameSamplerFactory samplerFactory)
    {
        _capturePort = capturePort;
        _detectorPort = detectorPort;
        _trackerPort = trackerPort;
        _embedderPort = embedderPort;
        _matcherPort = matcherPort;
        _publisher = publisher;
        _galleryProvider = galleryProvider;
        _evidenceFactory = evidenceFactory;
        _queue = queue;
        _metrics = metrics;
        _clock = clock;
        _latestCameraFrameStore = latestCameraFrameStore;
        _samplerFactory = samplerFactory;
    }

    public async Task CaptureAndQueueAsync(EdgeCameraOptions cameraOptions, int queueSize, CancellationToken cancellationToken)
    {
        _queue.ConfigureCamera(cameraOptions.CameraId, queueSize);

        var camera = new Camera(cameraOptions.SiteId, cameraOptions.Name, cameraOptions.Source);
        await using var stream = await _capturePort.CaptureFrameAsync(camera, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();

        var sequence = _sequences.TryGetValue(cameraOptions.CameraId, out var current) ? current + 1 : 1;
        _sequences[cameraOptions.CameraId] = sequence;
        var capturedAtUtc = _clock.UtcNow;
        _latestCameraFrameStore.Update(cameraOptions.CameraId, bytes, capturedAtUtc, sequence);

        var frame = new CapturedFrame(
            cameraOptions.CameraId,
            cameraOptions.SiteId,
            cameraOptions.ProtectedCaseId,
            bytes,
            capturedAtUtc,
            sequence);

        _queue.Enqueue(frame);
        _metrics.IncrementCounter("fps_in", ("camera", cameraOptions.CameraId.ToString()));
        _metrics.RecordGauge("queue_depth", _queue.GetDepth(cameraOptions.CameraId), ("camera", cameraOptions.CameraId.ToString()));
        _metrics.RecordGauge("frames_dropped", _queue.GetDroppedCount(cameraOptions.CameraId), ("camera", cameraOptions.CameraId.ToString()));
    }

    public async Task<bool> TryProcessNextAsync(EdgeCameraOptions cameraOptions, int targetFps, CancellationToken cancellationToken)
    {
        if (!_queue.TryDequeue(cameraOptions.CameraId, out var frame) || frame is null)
        {
            return false;
        }

        if (_galleryProvider.GetByScope(cameraOptions.ProtectedCaseId, cameraOptions.SiteId).Count == 0)
        {
            return false;
        }

        if (!_samplers.TryGetValue(cameraOptions.CameraId, out var sampler))
        {
            sampler = _samplerFactory.Create(targetFps);
            _samplers[cameraOptions.CameraId] = sampler;
        }

        if (!sampler.ShouldProcess(frame.CapturedAtUtc))
        {
            return false;
        }

        var decodeStarted = _clock.UtcNow;
        await using var source = new MemoryStream(frame.Bytes);
        var detected = await _detectorPort.DetectAsync(source, cancellationToken);
        _metrics.RecordLatency("decode_latency_ms", _clock.UtcNow - decodeStarted, ("camera", cameraOptions.CameraId.ToString()));

        var detectStarted = _clock.UtcNow;
        var tracked = await _trackerPort.TrackAsync(detected, cancellationToken);
        _metrics.RecordLatency("detect_latency_ms", _clock.UtcNow - detectStarted, ("camera", cameraOptions.CameraId.ToString()));

        foreach (var face in tracked)
        {
            var embedStarted = _clock.UtcNow;
            var embedding = await _embedderPort.CreateEmbeddingAsync(face, cancellationToken);
            _metrics.RecordLatency("embed_latency_ms", _clock.UtcNow - embedStarted, ("camera", cameraOptions.CameraId.ToString()));

            var matchStarted = _clock.UtcNow;
            var score = await _matcherPort.MatchAsync(embedding, cameraOptions.ProtectedCaseId, cancellationToken);
            _metrics.RecordLatency("match_latency_ms", _clock.UtcNow - matchStarted, ("camera", cameraOptions.CameraId.ToString()));

            if (_matcherPort is RestrictedGalleryMatcherPort scopeMatcher)
            {
                var match = scopeMatcher.MatchWithinScope(embedding, cameraOptions.ProtectedCaseId, cameraOptions.SiteId);
                if (!match.IsMatch || match.IsBystander)
                {
                    continue;
                }
            }

            var candidateEvent = new BiometricCandidateEvent(
                cameraOptions.ProtectedCaseId,
                new CameraScope(cameraOptions.SiteId, cameraOptions.CameraId),
                score,
                _clock.UtcNow);

            var evidences = _evidenceFactory.Create(cameraOptions.CameraId, frame.Bytes, face.CropBytes);
            await _publisher.PublishAsync(candidateEvent, evidences, cancellationToken);
            _metrics.IncrementCounter("candidate_events_total", ("camera", cameraOptions.CameraId.ToString()));
        }

        _metrics.IncrementCounter("fps_processed", ("camera", cameraOptions.CameraId.ToString()));
        _metrics.RecordGauge("queue_depth", _queue.GetDepth(cameraOptions.CameraId), ("camera", cameraOptions.CameraId.ToString()));
        return true;
    }
}

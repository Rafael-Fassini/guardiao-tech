using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
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
    private readonly BoundedCameraFrameQueue _queue;
    private readonly EdgeMetricsCollector _metrics;
    private readonly IClock _clock;
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
        BoundedCameraFrameQueue queue,
        EdgeMetricsCollector metrics,
        IClock clock,
        FrameSamplerFactory samplerFactory)
    {
        _capturePort = capturePort;
        _detectorPort = detectorPort;
        _trackerPort = trackerPort;
        _embedderPort = embedderPort;
        _matcherPort = matcherPort;
        _publisher = publisher;
        _queue = queue;
        _metrics = metrics;
        _clock = clock;
        _samplerFactory = samplerFactory;
    }

    public async Task CaptureAndQueueAsync(EdgeCameraOptions cameraOptions, int queueSize, CancellationToken cancellationToken)
    {
        _queue.ConfigureCamera(cameraOptions.CameraId, queueSize);

        var camera = new Camera(cameraOptions.SiteId, cameraOptions.Name, cameraOptions.Source);
        await using var stream = await _capturePort.CaptureFrameAsync(camera, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var sequence = _sequences.TryGetValue(cameraOptions.CameraId, out var current) ? current + 1 : 1;
        _sequences[cameraOptions.CameraId] = sequence;

        var frame = new CapturedFrame(
            cameraOptions.CameraId,
            cameraOptions.SiteId,
            cameraOptions.ProtectedCaseId,
            memory.ToArray(),
            _clock.UtcNow,
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

            var candidateEvent = new BiometricCandidateEvent(
                cameraOptions.ProtectedCaseId,
                new CameraScope(cameraOptions.SiteId, cameraOptions.CameraId),
                score,
                _clock.UtcNow);

            await _publisher.PublishAsync(candidateEvent, cancellationToken);
            _metrics.IncrementCounter("candidate_events_total", ("camera", cameraOptions.CameraId.ToString()));
        }

        _metrics.IncrementCounter("fps_processed", ("camera", cameraOptions.CameraId.ToString()));
        _metrics.RecordGauge("queue_depth", _queue.GetDepth(cameraOptions.CameraId), ("camera", cameraOptions.CameraId.ToString()));
        return true;
    }
}

using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Adapters;

public sealed class DeterministicFaceDetectorPort : IFaceDetectorPort
{
    private readonly double _minimumDetectionScore;

    public DeterministicFaceDetectorPort(IOptions<EdgeWorkerOptions> options)
    {
        _minimumDetectionScore = options.Value.MinimumDetectionScore;
    }

    public async Task<IReadOnlyCollection<DetectedFace>> DetectAsync(Stream frame, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await frame.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.Length == 0)
        {
            return [];
        }

        var score = Math.Min(0.99, Math.Max(0.10, bytes.Average(x => x / 255d)));
        if (score < _minimumDetectionScore)
        {
            return [];
        }

        return [new DetectedFace(Guid.NewGuid(), bytes)];
    }
}

public sealed class DeterministicFaceTrackerPort : IFaceTrackerPort
{
    public Task<IReadOnlyCollection<TrackedFace>> TrackAsync(IReadOnlyCollection<DetectedFace> faces, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<TrackedFace>>(
            [.. faces.Select(x => new TrackedFace(x.DetectionId, x.CropBytes))]);
    }
}

public sealed class DeterministicFaceEmbedderPort : IFaceEmbedderPort
{
    public Task<IReadOnlyCollection<float>> CreateEmbeddingAsync(TrackedFace face, CancellationToken cancellationToken = default)
    {
        var bytes = face.CropBytes.Length == 0 ? [128] : face.CropBytes;
        var vector = bytes
            .Take(16)
            .Select(x => x / 255f)
            .ToArray();

        if (vector.Length < 16)
        {
            vector = [.. vector, .. Enumerable.Repeat(vector.LastOrDefault(), 16 - vector.Length)];
        }

        return Task.FromResult<IReadOnlyCollection<float>>(EmbeddingVectorMath.Normalize(vector));
    }
}

public sealed class InMemoryCandidateEventPublisher : ICandidateEventPublisher
{
    private readonly List<BiometricCandidateEvent> _events = [];
    private readonly object _sync = new();

    public Task PublishAsync(
        BiometricCandidateEvent candidateEvent,
        IReadOnlyCollection<CandidateEventEvidencePayload>? evidences = null,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _events.Add(candidateEvent);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<BiometricCandidateEvent> Snapshot()
    {
        lock (_sync)
        {
            return _events.ToArray();
        }
    }
}

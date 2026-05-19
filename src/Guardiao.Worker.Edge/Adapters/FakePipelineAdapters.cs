using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Worker.Edge.Adapters;

public sealed class FakeFaceDetectorPort : IFaceDetectorPort
{
    public async Task<IReadOnlyCollection<DetectedFace>> DetectAsync(Stream frame, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        await frame.CopyToAsync(memory, cancellationToken);
        return [new DetectedFace(Guid.NewGuid(), memory.ToArray())];
    }
}

public sealed class FakeFaceTrackerPort : IFaceTrackerPort
{
    public Task<IReadOnlyCollection<TrackedFace>> TrackAsync(IReadOnlyCollection<DetectedFace> faces, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<TrackedFace>>(
            [.. faces.Select(x => new TrackedFace(x.DetectionId, x.CropBytes))]);
    }
}

public sealed class FakeFaceEmbedderPort : IFaceEmbedderPort
{
    public Task<IReadOnlyCollection<float>> CreateEmbeddingAsync(TrackedFace face, CancellationToken cancellationToken = default)
    {
        var embedding = face.CropBytes.Take(8).Select(x => x / 255f).DefaultIfEmpty(0.5f).ToArray();
        return Task.FromResult<IReadOnlyCollection<float>>(embedding);
    }
}

public sealed class FakeFaceMatcherPort : IFaceMatcherPort
{
    public Task<MatchScore> MatchAsync(IReadOnlyCollection<float> embedding, Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        var aggregate = embedding.DefaultIfEmpty(0.5f).Average();
        var score = Math.Max(0.5, Math.Min(0.99, aggregate + 0.4));
        return Task.FromResult(new MatchScore(score));
    }
}

public sealed class InMemoryCandidateEventPublisher : ICandidateEventPublisher
{
    private readonly List<BiometricCandidateEvent> _events = [];
    private readonly object _sync = new();

    public Task PublishAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default)
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

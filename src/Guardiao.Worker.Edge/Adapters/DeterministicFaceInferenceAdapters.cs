using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
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

        return Task.FromResult<IReadOnlyCollection<float>>(Normalize(vector));
    }

    private static float[] Normalize(float[] values)
    {
        var norm = MathF.Sqrt(values.Sum(x => x * x));
        if (norm == 0)
        {
            return values;
        }

        return values.Select(x => x / norm).ToArray();
    }
}

public sealed class RestrictedGalleryMatcherPort : IFaceMatcherPort
{
    private readonly IRestrictedGalleryProvider _galleryProvider;
    private readonly EdgeMetricsCollector _metrics;
    private readonly double _matchThreshold;

    public RestrictedGalleryMatcherPort(
        IRestrictedGalleryProvider galleryProvider,
        EdgeMetricsCollector metrics,
        IOptions<EdgeWorkerOptions> options)
    {
        _galleryProvider = galleryProvider;
        _metrics = metrics;
        _matchThreshold = options.Value.MatchThreshold;
    }

    public Task<MatchScore> MatchAsync(IReadOnlyCollection<float> embedding, Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        var gallery = _galleryProvider.GetByProtectedCase(protectedCaseId);
        if (gallery.Count == 0)
        {
            return Task.FromResult(new MatchScore(0));
        }

        var score = gallery
            .Where(x => !x.IsBystander)
            .Select(x => CosineSimilarity(embedding, x.Embedding))
            .DefaultIfEmpty(0)
            .Max();

        _metrics.RecordGauge("gallery_match_score", score, ("case", protectedCaseId.ToString()));
        return Task.FromResult(new MatchScore(score));
    }

    public GalleryMatchResult MatchWithinScope(IReadOnlyCollection<float> embedding, Guid protectedCaseId, Guid siteId)
    {
        var best = _galleryProvider.GetByScope(protectedCaseId, siteId)
            .Select(x => new
            {
                Candidate = x,
                Score = CosineSimilarity(embedding, x.Embedding)
            })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (best is null)
        {
            return new GalleryMatchResult(protectedCaseId, siteId, Guid.Empty, string.Empty, 0, false, false);
        }

        var isMatch = !best.Candidate.IsBystander && best.Score >= _matchThreshold;
        return new GalleryMatchResult(
            best.Candidate.ProtectedCaseId,
            best.Candidate.SiteId,
            best.Candidate.PersonProjectionId,
            best.Candidate.ExternalPersonId,
            best.Score,
            isMatch,
            best.Candidate.IsBystander);
    }

    private static double CosineSimilarity(IReadOnlyCollection<float> left, IReadOnlyCollection<float> right)
    {
        var leftArray = left.ToArray();
        var rightArray = right.ToArray();
        var length = Math.Min(leftArray.Length, rightArray.Length);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var i = 0; i < length; i++)
        {
            dot += leftArray[i] * rightArray[i];
            leftNorm += leftArray[i] * leftArray[i];
            rightNorm += rightArray[i] * rightArray[i];
        }

        if (leftNorm == 0 || rightNorm == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm));
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

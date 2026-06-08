using System.Text.Json;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Guardiao.IntegrationTests.Worker;

public sealed class ApprovedCameraReplayHarness
{
    public async Task<ApprovedCameraReplayResult> RunAsync(string fixturePath, CancellationToken cancellationToken = default)
    {
        await using var fixtureStream = File.OpenRead(fixturePath);
        var fixture = await JsonSerializer.DeserializeAsync<ApprovedCameraReplayFixture>(
            fixtureStream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken) ?? throw new InvalidOperationException("Replay fixture is invalid.");

        var publisher = new InMemoryCandidateEventPublisher();
        var metrics = new EdgeMetricsCollector();
        var options = new EdgeWorkerOptions
        {
            MatchThreshold = 0.1,
            MinimumDetectionScore = 0.1,
            RestrictedGallery =
            [
                .. fixture.RestrictedGallery.Select(x => new RestrictedGallerySeedOptions
                {
                    ProtectedCaseId = x.ProtectedCaseId,
                    SiteId = x.SiteId,
                    PersonProjectionId = x.PersonProjectionId,
                    ExternalPersonId = x.ExternalPersonId,
                    IsBystander = x.IsBystander,
                    Embedding = x.Embedding
                })
            ]
        };

        var galleryProvider = new RestrictedGalleryProvider(Options.Create(options));
        var session = new CameraPipelineSession(
            new ReplayCameraCapturePort(fixture.FramesBase64.Select(Convert.FromBase64String).ToArray()),
            new DeterministicFaceDetectorPort(Options.Create(options)),
            new DeterministicFaceTrackerPort(),
            new DeterministicFaceEmbedderPort(),
            new RestrictedGalleryMatcherPort(galleryProvider, metrics, Options.Create(options)),
            publisher,
            galleryProvider,
            new CandidateEventEvidenceFactory(
                Options.Create(options),
                metrics,
                NullLogger<CandidateEventEvidenceFactory>.Instance),
            new BoundedCameraFrameQueue(),
            metrics,
            new SystemClock(),
            new FrameSamplerFactory());

        var camera = new EdgeCameraOptions
        {
            CameraId = fixture.CameraId,
            SiteId = fixture.SiteId,
            ProtectedCaseId = fixture.ProtectedCaseId,
            Name = fixture.CameraName,
            Source = fixture.Source,
            Enabled = true
        };

        foreach (var _ in fixture.FramesBase64)
        {
            await session.CaptureAndQueueAsync(camera, 2, cancellationToken);
            await session.TryProcessNextAsync(camera, 4, cancellationToken);
        }

        return new ApprovedCameraReplayResult(
            fixture.Name,
            fixture.ExpectedCandidateEvents,
            publisher.Snapshot().Count,
            metrics.SnapshotCounters(),
            metrics.SnapshotGauges());
    }
}

public sealed record ApprovedCameraReplayResult(
    string Name,
    int ExpectedCandidateEvents,
    int PublishedCandidateEvents,
    IReadOnlyDictionary<string, double> Counters,
    IReadOnlyDictionary<string, double> Gauges);

public sealed class ApprovedCameraReplayFixture
{
    public string Name { get; set; } = string.Empty;
    public Guid CameraId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ProtectedCaseId { get; set; }
    public string CameraName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int ExpectedCandidateEvents { get; set; }
    public List<ApprovedRestrictedGalleryFixture> RestrictedGallery { get; set; } = [];
    public List<string> FramesBase64 { get; set; } = [];
}

public sealed class ApprovedRestrictedGalleryFixture
{
    public Guid ProtectedCaseId { get; set; }
    public Guid SiteId { get; set; }
    public Guid PersonProjectionId { get; set; }
    public string ExternalPersonId { get; set; } = string.Empty;
    public bool IsBystander { get; set; }
    public float[] Embedding { get; set; } = [];
}

internal sealed class ReplayCameraCapturePort : ICameraCapturePort
{
    private readonly Queue<byte[]> _frames;

    public ReplayCameraCapturePort(IEnumerable<byte[]> frames)
    {
        _frames = new Queue<byte[]>(frames);
    }

    public Task<Stream> CaptureFrameAsync(Guardiao.Domain.Entities.Camera camera, CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
        {
            throw new InvalidOperationException("Replay fixture has no remaining frames.");
        }

        return Task.FromResult<Stream>(new MemoryStream(_frames.Dequeue(), writable: false));
    }
}

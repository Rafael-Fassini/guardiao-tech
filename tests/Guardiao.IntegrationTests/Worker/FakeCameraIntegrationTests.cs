using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.IntegrationTests.Worker;

public class FakeCameraIntegrationTests
{
    [Fact]
    public async Task WebcamCapturePipeline_ShouldPublishCandidateEvent()
    {
        var publisher = new InMemoryCandidateEventPublisher();
        var metrics = new EdgeMetricsCollector();
        var galleryProvider = new RestrictedGalleryProvider(Options.Create(new EdgeWorkerOptions
        {
            RestrictedGallery =
            [
                new RestrictedGallerySeedOptions
                {
                    ProtectedCaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    SiteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    PersonProjectionId = Guid.NewGuid(),
                    ExternalPersonId = "person-1",
                    IsBystander = false,
                    Embedding = Enumerable.Repeat(0.25f, 16).ToArray()
                }
            ]
        }));
        var session = new CameraPipelineSession(
            new AdaptiveCameraCaptureAdapter(),
            new DeterministicFaceDetectorPort(Options.Create(new EdgeWorkerOptions { MinimumDetectionScore = 0.1 })),
            new DeterministicFaceTrackerPort(),
            new DeterministicFaceEmbedderPort(),
            new RestrictedGalleryMatcherPort(galleryProvider, metrics, Options.Create(new EdgeWorkerOptions { MatchThreshold = 0.1 })),
            publisher,
            galleryProvider,
            new CandidateEventEvidenceFactory(
                Options.Create(new EdgeWorkerOptions()),
                metrics,
                NullLogger<CandidateEventEvidenceFactory>.Instance),
            new BoundedCameraFrameQueue(),
            metrics,
            new SystemClock(),
            new FrameSamplerFactory());

        var camera = new EdgeCameraOptions
        {
            CameraId = Guid.NewGuid(),
            SiteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ProtectedCaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = "Webcam",
            Source = "webcam://0",
            Enabled = true
        };

        await session.CaptureAndQueueAsync(camera, 2, CancellationToken.None);
        await session.TryProcessNextAsync(camera, 4, CancellationToken.None);

        Assert.Single(publisher.Snapshot());
    }
}

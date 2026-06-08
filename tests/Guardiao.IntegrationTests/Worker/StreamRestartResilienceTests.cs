using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.IntegrationTests.Worker;

public class StreamRestartResilienceTests
{
    [Fact]
    public async Task CaptureLoop_ShouldRecover_AfterTransientFailure()
    {
        var flakyCapture = new FlakyCapturePort();
        var publisher = new InMemoryCandidateEventPublisher();
        var metrics = new EdgeMetricsCollector();
        var protectedCaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var siteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var galleryProvider = new RestrictedGalleryProvider(Options.Create(new EdgeWorkerOptions
        {
            RestrictedGallery =
            [
                new RestrictedGallerySeedOptions
                {
                    ProtectedCaseId = protectedCaseId,
                    SiteId = siteId,
                    PersonProjectionId = Guid.NewGuid(),
                    ExternalPersonId = "person-1",
                    IsBystander = false,
                    Embedding = Enumerable.Repeat(0.25f, 16).ToArray()
                }
            ]
        }));
        var session = new CameraPipelineSession(
            flakyCapture,
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
            SiteId = siteId,
            ProtectedCaseId = protectedCaseId,
            Name = "Flaky Webcam",
            Source = "webcam://0",
            Enabled = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAndQueueAsync(camera, 2, CancellationToken.None));
        await session.CaptureAndQueueAsync(camera, 2, CancellationToken.None);
        var processed = await session.TryProcessNextAsync(camera, 4, CancellationToken.None);

        Assert.True(processed);
        Assert.Single(publisher.Snapshot());
    }

    private sealed class FlakyCapturePort : ICameraCapturePort
    {
        private bool _failedOnce;

        public Task<Stream> CaptureFrameAsync(Camera camera, CancellationToken cancellationToken = default)
        {
            if (!_failedOnce)
            {
                _failedOnce = true;
                throw new InvalidOperationException("simulated capture failure");
            }

            return Task.FromResult<Stream>(new MemoryStream([1, 2, 3, 4]));
        }
    }
}

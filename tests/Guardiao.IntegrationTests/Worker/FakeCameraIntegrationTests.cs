using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
using Xunit;

namespace Guardiao.IntegrationTests.Worker;

public class FakeCameraIntegrationTests
{
    [Fact]
    public async Task WebcamCapturePipeline_ShouldPublishCandidateEvent()
    {
        var publisher = new InMemoryCandidateEventPublisher();
        var metrics = new EdgeMetricsCollector();
        var session = new CameraPipelineSession(
            new AdaptiveCameraCaptureAdapter(),
            new FakeFaceDetectorPort(),
            new FakeFaceTrackerPort(),
            new FakeFaceEmbedderPort(),
            new FakeFaceMatcherPort(),
            publisher,
            new BoundedCameraFrameQueue(),
            metrics,
            new SystemClock(),
            new FrameSamplerFactory());

        var camera = new EdgeCameraOptions
        {
            CameraId = Guid.NewGuid(),
            SiteId = Guid.NewGuid(),
            ProtectedCaseId = Guid.NewGuid(),
            Name = "Webcam",
            Source = "webcam://0",
            Enabled = true
        };

        await session.CaptureAndQueueAsync(camera, 2, CancellationToken.None);
        await session.TryProcessNextAsync(camera, 4, CancellationToken.None);

        Assert.Single(publisher.Snapshot());
    }
}

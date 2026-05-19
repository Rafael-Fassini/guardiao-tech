using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Guardiao.Worker.Edge.Services;
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
        var session = new CameraPipelineSession(
            flakyCapture,
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

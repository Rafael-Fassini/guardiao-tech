using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class WorkerOperationalStateTests
{
    [Fact]
    public void Snapshot_ShouldBeNotReady_WhenGalleryWasNeverRefreshed()
    {
        var cameraId = Guid.NewGuid();
        var state = CreateState(cameraId);

        state.RecordCameraSuccess(cameraId, DateTime.UtcNow);
        var snapshot = state.Snapshot(DateTime.UtcNow, 30);

        Assert.False(snapshot.IsReady);
    }

    [Fact]
    public void Snapshot_ShouldBeReady_WhenCameraAndGalleryAreFresh()
    {
        var cameraId = Guid.NewGuid();
        var state = CreateState(cameraId);

        state.RecordCameraSuccess(cameraId, DateTime.UtcNow);
        state.RecordGalleryRefresh(1, DateTime.UtcNow);

        var snapshot = state.Snapshot(DateTime.UtcNow, 30);

        Assert.True(snapshot.IsReady);
        Assert.Empty(snapshot.StaleCameraIds);
    }

    private static WorkerOperationalState CreateState(Guid cameraId)
    {
        return new WorkerOperationalState(Options.Create(new EdgeWorkerOptions
        {
            Cameras =
            [
                new EdgeCameraOptions
                {
                    CameraId = cameraId,
                    SiteId = Guid.NewGuid(),
                    ProtectedCaseId = Guid.NewGuid(),
                    Name = "Cam 1",
                    Source = "webcam://0",
                    Enabled = true
                }
            ]
        }));
    }
}

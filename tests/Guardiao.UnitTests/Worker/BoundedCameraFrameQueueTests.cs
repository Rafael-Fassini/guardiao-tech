using Guardiao.Worker.Edge.Pipeline;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class BoundedCameraFrameQueueTests
{
    [Fact]
    public void Enqueue_ShouldDropOldest_WhenQueueIsFull()
    {
        var queue = new BoundedCameraFrameQueue();
        var cameraId = Guid.NewGuid();
        queue.ConfigureCamera(cameraId, 2);

        queue.Enqueue(new CapturedFrame(cameraId, Guid.NewGuid(), Guid.NewGuid(), [1], DateTime.UtcNow, 1));
        queue.Enqueue(new CapturedFrame(cameraId, Guid.NewGuid(), Guid.NewGuid(), [2], DateTime.UtcNow, 2));
        queue.Enqueue(new CapturedFrame(cameraId, Guid.NewGuid(), Guid.NewGuid(), [3], DateTime.UtcNow, 3));

        Assert.Equal(2, queue.GetDepth(cameraId));
        Assert.Equal(1, queue.GetDroppedCount(cameraId));

        Assert.True(queue.TryDequeue(cameraId, out var first));
        Assert.NotNull(first);
        Assert.Equal(2, first!.Sequence);
    }
}

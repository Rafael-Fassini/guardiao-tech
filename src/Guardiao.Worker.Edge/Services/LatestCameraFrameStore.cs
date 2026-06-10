using System.Collections.Concurrent;

namespace Guardiao.Worker.Edge.Services;

public sealed class LatestCameraFrameStore
{
    private readonly ConcurrentDictionary<Guid, CameraFrameSnapshot> _frames = new();

    public void Update(Guid cameraId, byte[] content, DateTime capturedAtUtc, long sequence, string contentType = "image/jpeg")
    {
        _frames[cameraId] = new CameraFrameSnapshot(cameraId, content, contentType, capturedAtUtc, sequence);
    }

    public bool TryGet(Guid cameraId, out CameraFrameSnapshot snapshot)
    {
        if (_frames.TryGetValue(cameraId, out var value))
        {
            snapshot = value;
            return true;
        }

        snapshot = default!;
        return false;
    }
}

public sealed record CameraFrameSnapshot(
    Guid CameraId,
    byte[] Content,
    string ContentType,
    DateTime CapturedAtUtc,
    long Sequence);

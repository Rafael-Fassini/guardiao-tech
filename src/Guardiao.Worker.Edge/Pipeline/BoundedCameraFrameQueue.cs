using System.Collections.Concurrent;

namespace Guardiao.Worker.Edge.Pipeline;

public sealed class BoundedCameraFrameQueue
{
    private readonly ConcurrentDictionary<Guid, CameraFrameBuffer> _buffers = new();

    public void ConfigureCamera(Guid cameraId, int capacity)
    {
        _buffers[cameraId] = new CameraFrameBuffer(capacity);
    }

    public void Enqueue(CapturedFrame frame)
    {
        var buffer = _buffers.GetOrAdd(frame.CameraId, _ => new CameraFrameBuffer(2));
        buffer.Enqueue(frame);
    }

    public bool TryDequeue(Guid cameraId, out CapturedFrame? frame)
    {
        if (_buffers.TryGetValue(cameraId, out var buffer))
        {
            return buffer.TryDequeue(out frame);
        }

        frame = null;
        return false;
    }

    public int GetDepth(Guid cameraId)
    {
        return _buffers.TryGetValue(cameraId, out var buffer) ? buffer.Count : 0;
    }

    public long GetDroppedCount(Guid cameraId)
    {
        return _buffers.TryGetValue(cameraId, out var buffer) ? buffer.DroppedCount : 0;
    }
}

internal sealed class CameraFrameBuffer
{
    private readonly object _sync = new();
    private readonly Queue<CapturedFrame> _queue = [];

    public CameraFrameBuffer(int capacity)
    {
        Capacity = capacity;
    }

    public int Capacity { get; }
    public long DroppedCount { get; private set; }
    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _queue.Count;
            }
        }
    }

    public void Enqueue(CapturedFrame frame)
    {
        lock (_sync)
        {
            if (_queue.Count >= Capacity)
            {
                _queue.Dequeue();
                DroppedCount++;
            }

            _queue.Enqueue(frame);
        }
    }

    public bool TryDequeue(out CapturedFrame? frame)
    {
        lock (_sync)
        {
            if (_queue.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _queue.Dequeue();
            return true;
        }
    }
}

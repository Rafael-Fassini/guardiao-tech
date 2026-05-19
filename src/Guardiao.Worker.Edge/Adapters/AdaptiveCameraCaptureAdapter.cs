using System.Text;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;

namespace Guardiao.Worker.Edge.Adapters;

public sealed class AdaptiveCameraCaptureAdapter : ICameraCapturePort
{
    public async Task<Stream> CaptureFrameAsync(Camera camera, CancellationToken cancellationToken = default)
    {
        if (camera.StreamEndpoint.StartsWith("webcam://", StringComparison.OrdinalIgnoreCase))
        {
            return await CaptureWebcamAsync(camera, cancellationToken);
        }

        if (camera.StreamEndpoint.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
        {
            return await CaptureRtspAsync(camera, cancellationToken);
        }

        throw new InvalidOperationException($"Unsupported camera source '{camera.StreamEndpoint}'.");
    }

    private static Task<Stream> CaptureWebcamAsync(Camera camera, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes($"webcam-frame:{camera.Id}:{DateTime.UtcNow:O}");
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }

    private static Task<Stream> CaptureRtspAsync(Camera camera, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes($"rtsp-frame:{camera.Id}:{DateTime.UtcNow:O}");
        return Task.FromResult<Stream>(new MemoryStream(bytes));
    }
}

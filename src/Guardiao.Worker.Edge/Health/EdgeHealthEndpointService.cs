using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Health;

public sealed class EdgeHealthEndpointService : BackgroundService
{
    private const string PreviewAuthHeaderName = "X-Worker-Preview-Auth";
    private readonly EdgeMetricsCollector _metrics;
    private readonly EdgeWorkerOptions _options;
    private readonly WorkerOperationalState _state;
    private readonly LatestCameraFrameStore _latestCameraFrameStore;
    private readonly int _port;
    private readonly TcpListener _listener;

    public EdgeHealthEndpointService(
        IOptions<EdgeWorkerOptions> options,
        EdgeMetricsCollector metrics,
        WorkerOperationalState state,
        LatestCameraFrameStore latestCameraFrameStore)
    {
        _metrics = metrics;
        _options = options.Value;
        _state = state;
        _latestCameraFrameStore = latestCameraFrameStore;
        _port = _options.HealthPort;
        _listener = new TcpListener(IPAddress.Any, _port);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(() => HandleAsync(client, stoppingToken), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during host shutdown.
        }
        catch (SocketException) when (stoppingToken.IsCancellationRequested)
        {
            // Listener is stopped during host shutdown.
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener.Stop();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var requestLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(requestLine))
        {
            await WriteResponseAsync(stream, 400, new { error = "Bad Request" }, cancellationToken);
            return;
        }

        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(line))
            {
                var separatorIndex = line.IndexOf(':');
                if (separatorIndex > 0)
                {
                    var headerName = line[..separatorIndex].Trim();
                    var headerValue = line[(separatorIndex + 1)..].Trim();
                    headers[headerName] = headerValue;
                }
            }
        }
        while (!string.IsNullOrEmpty(line));

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var path = parts.Length >= 2 ? parts[1] : "/";

        if (TryParseCameraPath(path, "preview", out var cameraId))
        {
            if (!IsPreviewAuthorized(headers))
            {
                await WriteResponseAsync(stream, 401, new { error = "Unauthorized" }, cancellationToken);
                return;
            }

            if (!_latestCameraFrameStore.TryGet(cameraId, out var snapshot))
            {
                await WriteResponseAsync(stream, 404, new { error = "PreviewUnavailable" }, cancellationToken);
                return;
            }

            await WriteBinaryResponseAsync(
                stream,
                200,
                snapshot.Content,
                snapshot.ContentType,
                cancellationToken,
                ("Cache-Control", "no-store, no-cache, must-revalidate"),
                ("X-Captured-At-Utc", snapshot.CapturedAtUtc.ToString("O")),
                ("X-Frame-Sequence", snapshot.Sequence.ToString()));
            return;
        }

        if (TryParseCameraPath(path, "live", out cameraId))
        {
            if (!IsPreviewAuthorized(headers))
            {
                await WriteResponseAsync(stream, 401, new { error = "Unauthorized" }, cancellationToken);
                return;
            }

            var initialSnapshot = await WaitForInitialSnapshotAsync(cameraId, cancellationToken);
            if (initialSnapshot is null)
            {
                await WriteResponseAsync(stream, 404, new { error = "PreviewUnavailable" }, cancellationToken);
                return;
            }

            await StreamLivePreviewAsync(stream, cameraId, initialSnapshot, cancellationToken);
            return;
        }

        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 200, new { status = "Healthy" }, cancellationToken);
            return;
        }

        if (path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await WriteResponseAsync(stream, 200, new
            {
                counters = _metrics.SnapshotCounters(),
                gauges = _metrics.SnapshotGauges()
            }, cancellationToken);
            return;
        }

        if (path.Equals("/ready", StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = _state.Snapshot(DateTime.UtcNow, _options.GalleryRefreshIntervalSeconds);
            await WriteResponseAsync(
                stream,
                snapshot.IsReady ? 200 : 503,
                new
                {
                    status = snapshot.IsReady ? "Ready" : "NotReady",
                    enabledCameraCount = snapshot.EnabledCameraCount,
                    expectedScopeCount = snapshot.ExpectedScopeCount,
                    cachedScopeCount = snapshot.CachedScopeCount,
                    lastGalleryRefreshSuccessUtc = snapshot.LastGalleryRefreshSuccessUtc,
                    lastGalleryRefreshFailureUtc = snapshot.LastGalleryRefreshFailureUtc,
                    staleCameraIds = snapshot.StaleCameraIds,
                    cameraFailureCounts = snapshot.CameraFailureCounts
                },
                cancellationToken);
            return;
        }

        await WriteResponseAsync(stream, 404, new { error = "Not Found" }, cancellationToken);
    }

    private bool IsPreviewAuthorized(IReadOnlyDictionary<string, string> headers)
    {
        return headers.TryGetValue(PreviewAuthHeaderName, out var suppliedSecret) &&
               string.Equals(suppliedSecret, _options.ApiSharedSecret, StringComparison.Ordinal);
    }

    private async Task<CameraFrameSnapshot?> WaitForInitialSnapshotAsync(Guid cameraId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (_latestCameraFrameStore.TryGet(cameraId, out var snapshot))
            {
                return snapshot;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        }

        return null;
    }

    private async Task StreamLivePreviewAsync(
        NetworkStream stream,
        Guid cameraId,
        CameraFrameSnapshot initialSnapshot,
        CancellationToken cancellationToken)
    {
        const string boundary = "guardiao-frame";

        var headers = new List<string>
        {
            "HTTP/1.1 200 OK",
            $"Content-Type: multipart/x-mixed-replace; boundary={boundary}",
            "Cache-Control: no-store, no-cache, must-revalidate",
            "Pragma: no-cache",
            "Connection: close",
            string.Empty,
            string.Empty
        };

        var headerBytes = Encoding.ASCII.GetBytes(string.Join("\r\n", headers));
        await stream.WriteAsync(headerBytes, cancellationToken);
        await WriteMultipartFrameAsync(stream, boundary, initialSnapshot, cancellationToken);

        var lastSequence = initialSnapshot.Sequence;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_latestCameraFrameStore.TryGet(cameraId, out var snapshot) &&
                    snapshot.Sequence != lastSequence)
                {
                    await WriteMultipartFrameAsync(stream, boundary, snapshot, cancellationToken);
                    lastSequence = snapshot.Sequence;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(120), cancellationToken);
            }
        }
        catch (IOException)
        {
            // Expected when the client disconnects during streaming.
        }
        catch (ObjectDisposedException)
        {
            // Expected when the connection closes during streaming.
        }
    }

    private static async Task WriteMultipartFrameAsync(
        NetworkStream stream,
        string boundary,
        CameraFrameSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var partHeaders = new StringBuilder()
            .Append("--").Append(boundary).Append("\r\n")
            .Append("Content-Type: ").Append(snapshot.ContentType).Append("\r\n")
            .Append("Content-Length: ").Append(snapshot.Content.Length).Append("\r\n")
            .Append("X-Captured-At-Utc: ").Append(snapshot.CapturedAtUtc.ToString("O")).Append("\r\n")
            .Append("X-Frame-Sequence: ").Append(snapshot.Sequence).Append("\r\n")
            .Append("\r\n")
            .ToString();

        await stream.WriteAsync(Encoding.ASCII.GetBytes(partHeaders), cancellationToken);
        await stream.WriteAsync(snapshot.Content, cancellationToken);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"), cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static bool TryParseCameraPath(string path, string terminalSegment, out Guid cameraId)
    {
        cameraId = Guid.Empty;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3 &&
               string.Equals(segments[0], "cameras", StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(segments[1], out cameraId) &&
               string.Equals(segments[2], terminalSegment, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, object payload, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        var headers = string.Join("\r\n", new[]
        {
            $"HTTP/1.1 {statusCode} {ReasonPhrase(statusCode)}",
            "Content-Type: application/json",
            $"Content-Length: {body.Length}",
            "Connection: close",
            string.Empty,
            string.Empty
        });

        var headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task WriteBinaryResponseAsync(
        NetworkStream stream,
        int statusCode,
        byte[] body,
        string contentType,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] additionalHeaders)
    {
        var headers = new List<string>
        {
            $"HTTP/1.1 {statusCode} {ReasonPhrase(statusCode)}",
            $"Content-Type: {contentType}",
            $"Content-Length: {body.Length}",
            "Connection: close"
        };

        foreach (var (name, value) in additionalHeaders)
        {
            headers.Add($"{name}: {value}");
        }

        headers.Add(string.Empty);
        headers.Add(string.Empty);

        var headerBytes = Encoding.ASCII.GetBytes(string.Join("\r\n", headers));
        await stream.WriteAsync(headerBytes, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        503 => "Service Unavailable",
        400 => "Bad Request",
        401 => "Unauthorized",
        404 => "Not Found",
        _ => "OK"
    };
}

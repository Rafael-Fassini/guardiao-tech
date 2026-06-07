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
    private readonly EdgeMetricsCollector _metrics;
    private readonly int _port;
    private readonly TcpListener _listener;

    public EdgeHealthEndpointService(IOptions<EdgeWorkerOptions> options, EdgeMetricsCollector metrics)
    {
        _metrics = metrics;
        _port = options.Value.HealthPort;
        _listener = new TcpListener(IPAddress.Any, _port);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(stoppingToken);
            _ = Task.Run(() => HandleAsync(client, stoppingToken), stoppingToken);
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
        }
        while (!string.IsNullOrEmpty(line));

        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var path = parts.Length >= 2 ? parts[1] : "/";

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

        await WriteResponseAsync(stream, 404, new { error = "Not Found" }, cancellationToken);
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

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        400 => "Bad Request",
        404 => "Not Found",
        _ => "OK"
    };
}

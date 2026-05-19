using System.Net;
using System.Text.Json;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Health;

public sealed class EdgeHealthEndpointService : BackgroundService
{
    private readonly EdgeMetricsCollector _metrics;
    private readonly int _port;
    private readonly HttpListener _listener = new();

    public EdgeHealthEndpointService(IOptions<EdgeWorkerOptions> options, EdgeMetricsCollector metrics)
    {
        _metrics = metrics;
        _port = options.Value.HealthPort;
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.Start();

        while (!stoppingToken.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync();
            _ = Task.Run(() => HandleAsync(context, stoppingToken), stoppingToken);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener.Stop();
        _listener.Close();
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, new { status = "Healthy" }, cancellationToken);
            return;
        }

        if (path.Equals("/metrics", StringComparison.OrdinalIgnoreCase))
        {
            await WriteJsonAsync(context, new
            {
                counters = _metrics.SnapshotCounters(),
                gauges = _metrics.SnapshotGauges()
            }, cancellationToken);
            return;
        }

        context.Response.StatusCode = 404;
        context.Response.Close();
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, object payload, CancellationToken cancellationToken)
    {
        context.Response.ContentType = "application/json";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();
    }
}

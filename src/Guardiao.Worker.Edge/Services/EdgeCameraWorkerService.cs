using Guardiao.Worker.Edge.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public sealed class EdgeCameraWorkerService : BackgroundService
{
    private readonly EdgeWorkerOptions _options;
    private readonly CameraPipelineSession _pipelineSession;
    private readonly EdgeMetricsCollector _metrics;
    private readonly WorkerOperationalState _state;
    private readonly ILogger<EdgeCameraWorkerService> _logger;

    public EdgeCameraWorkerService(
        IOptions<EdgeWorkerOptions> options,
        CameraPipelineSession pipelineSession,
        EdgeMetricsCollector metrics,
        WorkerOperationalState state,
        ILogger<EdgeCameraWorkerService> logger)
    {
        _options = options.Value;
        _pipelineSession = pipelineSession;
        _metrics = metrics;
        _state = state;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tasks = _options.Cameras
            .Where(x => x.Enabled)
            .Select(camera => RunCameraLoopAsync(camera, stoppingToken));

        return Task.WhenAll(tasks);
    }

    private async Task RunCameraLoopAsync(EdgeCameraOptions cameraOptions, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _pipelineSession.CaptureAndQueueAsync(cameraOptions, _options.QueueSizePerCamera, cancellationToken);
                await _pipelineSession.TryProcessNextAsync(cameraOptions, _options.ProcessingTargetFps, cancellationToken);
                _state.RecordCameraSuccess(cameraOptions.CameraId, DateTime.UtcNow);
                _metrics.IncrementCounter("camera_loop_success_total", ("camera", cameraOptions.CameraId.ToString()));
                await Task.Delay(TimeSpan.FromSeconds(1d / _options.IngressTargetFps), cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Camera loop failed for {CameraId}. Reconnecting.", cameraOptions.CameraId);
                _metrics.IncrementCounter("reconnect_count", ("camera", cameraOptions.CameraId.ToString()));
                _metrics.IncrementCounter("camera_loop_failures_total", ("camera", cameraOptions.CameraId.ToString()));
                _state.RecordCameraFailure(cameraOptions.CameraId, DateTime.UtcNow);
                await Task.Delay(_options.ReconnectDelayMilliseconds, cancellationToken);
            }
        }
    }
}

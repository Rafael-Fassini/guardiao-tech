using System.Collections.Concurrent;
using System.Diagnostics;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using OpenCvSharp;

namespace Guardiao.Worker.Edge.Adapters;

public sealed class AdaptiveCameraCaptureAdapter : ICameraCapturePort, IDisposable
{
    private readonly ILogger<AdaptiveCameraCaptureAdapter> _logger;
    private readonly ConcurrentDictionary<string, VideoCaptureSession> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public AdaptiveCameraCaptureAdapter(ILogger<AdaptiveCameraCaptureAdapter> logger)
    {
        _logger = logger;
    }

    public async Task<Stream> CaptureFrameAsync(Camera camera, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var session = _sessions.GetOrAdd(
            camera.StreamEndpoint,
            static (streamEndpoint, logger) => VideoCaptureSession.Create(streamEndpoint, logger),
            _logger);

        try
        {
            return await session.CaptureAsync(camera, cancellationToken);
        }
        catch
        {
            if (_sessions.TryRemove(camera.StreamEndpoint, out var failedSession))
            {
                failedSession.Dispose();
            }

            throw;
        }
    }

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        _sessions.Clear();
    }

    private static CaptureSourceDescriptor ParseSource(string streamEndpoint)
    {
        if (streamEndpoint.StartsWith("webcam://", StringComparison.OrdinalIgnoreCase))
        {
            var indexLiteral = streamEndpoint["webcam://".Length..];
            if (!int.TryParse(indexLiteral, out var deviceIndex) || deviceIndex < 0)
            {
                throw new InvalidOperationException($"Invalid webcam source '{streamEndpoint}'. Expected format webcam://<deviceIndex>.");
            }

            return new CaptureSourceDescriptor(CaptureSourceKind.Webcam, streamEndpoint, deviceIndex, string.Empty);
        }

        if (streamEndpoint.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
        {
            return new CaptureSourceDescriptor(CaptureSourceKind.Rtsp, streamEndpoint, null, streamEndpoint);
        }

        throw new InvalidOperationException($"Unsupported camera source '{streamEndpoint}'.");
    }

    private enum CaptureSourceKind
    {
        Webcam,
        Rtsp
    }

    private sealed record CaptureSourceDescriptor(
        CaptureSourceKind Kind,
        string DisplayValue,
        int? DeviceIndex,
        string StreamUrl);

    private sealed class VideoCaptureSession : IDisposable
    {
        private static readonly TimeSpan FfmpegCaptureCacheWindow = TimeSpan.FromMilliseconds(250);
        private readonly CaptureSourceDescriptor _source;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _mutex = new(1, 1);
        private VideoCapture? _capture;
        private bool _preferFfmpeg;
        private byte[]? _cachedFfmpegFrame;
        private DateTime _cachedFfmpegFrameCapturedAtUtc;

        private VideoCaptureSession(CaptureSourceDescriptor source, ILogger logger)
        {
            _source = source;
            _logger = logger;
        }

        public static VideoCaptureSession Create(string streamEndpoint, ILogger logger)
        {
            return new VideoCaptureSession(ParseSource(streamEndpoint), logger);
        }

        public async Task<Stream> CaptureAsync(Camera camera, CancellationToken cancellationToken)
        {
            await _mutex.WaitAsync(cancellationToken);

            try
            {
                if (_preferFfmpeg)
                {
                    return await CaptureWithFfmpegAsync(cancellationToken);
                }

                try
                {
                    using var frame = await Task.Run(ReadFrame, cancellationToken);
                    if (!Cv2.ImEncode(".jpg", frame, out var bytes))
                    {
                        throw new InvalidOperationException($"Could not encode a frame from '{camera.StreamEndpoint}'.");
                    }

                    return new MemoryStream(bytes, writable: false);
                }
                catch (EntryPointNotFoundException ex)
                {
                    _preferFfmpeg = true;
                    ResetCapture();
                    _logger.LogWarning(
                        ex,
                        "OpenCvSharp video capture entry points are unavailable for {StreamEndpoint}. Falling back to ffmpeg frame capture.",
                        _source.DisplayValue);

                    return await CaptureWithFfmpegAsync(cancellationToken);
                }
                catch (DllNotFoundException ex)
                {
                    _preferFfmpeg = true;
                    ResetCapture();
                    _logger.LogWarning(
                        ex,
                        "OpenCvSharp native runtime is unavailable for {StreamEndpoint}. Falling back to ffmpeg frame capture.",
                        _source.DisplayValue);

                    return await CaptureWithFfmpegAsync(cancellationToken);
                }
            }
            finally
            {
                _mutex.Release();
            }
        }

        public void Dispose()
        {
            _capture?.Dispose();
            _mutex.Dispose();
        }

        private async Task<Stream> CaptureWithFfmpegAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            if (_cachedFfmpegFrame is not null && now - _cachedFfmpegFrameCapturedAtUtc < FfmpegCaptureCacheWindow)
            {
                return new MemoryStream(_cachedFfmpegFrame, writable: false);
            }

            using var process = StartFfmpegProcess();
            process.Start();

            using var stdout = new MemoryStream();
            var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdout, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var waitTask = process.WaitForExitAsync(cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask, waitTask);

            var frameBytes = stdout.ToArray();
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"ffmpeg failed to capture a frame from '{_source.DisplayValue}' (exit code {process.ExitCode}). {stderr}".Trim());
            }

            if (frameBytes.Length == 0)
            {
                throw new InvalidOperationException($"ffmpeg returned an empty frame for '{_source.DisplayValue}'. {stderr}".Trim());
            }

            _cachedFfmpegFrame = frameBytes;
            _cachedFfmpegFrameCapturedAtUtc = now;
            return new MemoryStream(frameBytes, writable: false);
        }

        private Mat ReadFrame()
        {
            EnsureCaptureOpened();

            if (TryReadFrame(out var firstFrame))
            {
                return firstFrame;
            }

            _logger.LogWarning(
                "Camera frame read failed for {StreamEndpoint}. Reopening capture and retrying once.",
                _source.DisplayValue);

            ResetCapture();
            EnsureCaptureOpened();

            if (TryReadFrame(out var retryFrame))
            {
                return retryFrame;
            }

            throw new InvalidOperationException($"Could not read a frame from '{_source.DisplayValue}'.");
        }

        private bool TryReadFrame(out Mat frame)
        {
            frame = new Mat();
            var read = _capture!.Read(frame);
            if (!read || frame.Empty())
            {
                frame.Dispose();
                frame = null!;
                return false;
            }

            return true;
        }

        private void EnsureCaptureOpened()
        {
            if (_capture?.IsOpened() == true)
            {
                return;
            }

            _capture?.Dispose();
            _capture = _source.Kind switch
            {
                CaptureSourceKind.Webcam => CreateWebcamCapture(_source.DeviceIndex!.Value),
                CaptureSourceKind.Rtsp => CreateRtspCapture(_source.StreamUrl),
                _ => null
            };

            if (_capture is null || !_capture.IsOpened())
            {
                _capture?.Dispose();
                _capture = null;
                throw new InvalidOperationException($"Could not open camera source '{_source.DisplayValue}'.");
            }

            TrySetLowLatencyBuffer(_capture);
            _logger.LogInformation("Camera source opened for {StreamEndpoint}.", _source.DisplayValue);
        }

        private void ResetCapture()
        {
            _capture?.Release();
            _capture?.Dispose();
            _capture = null;
        }

        private static void TrySetLowLatencyBuffer(VideoCapture capture)
        {
            try
            {
                capture.Set(VideoCaptureProperties.BufferSize, 1);
            }
            catch
            {
                // BufferSize is backend-dependent. Ignore when unsupported.
            }
        }

        private static bool OpenRtspWithFallback(VideoCapture capture, string streamUrl)
        {
            return capture.Open(streamUrl, VideoCaptureAPIs.FFMPEG)
                || capture.Open(streamUrl, VideoCaptureAPIs.ANY);
        }

        private static VideoCapture CreateWebcamCapture(int deviceIndex)
        {
            return new VideoCapture(deviceIndex, VideoCaptureAPIs.ANY);
        }

        private static VideoCapture? CreateRtspCapture(string streamUrl)
        {
            var ffmpegCapture = new VideoCapture(streamUrl, VideoCaptureAPIs.FFMPEG);
            if (ffmpegCapture.IsOpened())
            {
                return ffmpegCapture;
            }

            ffmpegCapture.Dispose();

            var fallbackCapture = new VideoCapture(streamUrl, VideoCaptureAPIs.ANY);
            if (fallbackCapture.IsOpened())
            {
                return fallbackCapture;
            }

            fallbackCapture.Dispose();
            return null;
        }

        private Process StartFfmpegProcess()
        {
            var startInfo = new ProcessStartInfo("ffmpeg")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in BuildFfmpegArguments())
            {
                startInfo.ArgumentList.Add(argument);
            }

            return new Process
            {
                StartInfo = startInfo
            };
        }

        private IEnumerable<string> BuildFfmpegArguments()
        {
            yield return "-hide_banner";
            yield return "-loglevel";
            yield return "error";

            if (_source.Kind == CaptureSourceKind.Webcam)
            {
                yield return "-f";
                yield return "video4linux2";
                yield return "-i";
                yield return $"/dev/video{_source.DeviceIndex!.Value}";
            }
            else
            {
                yield return "-rtsp_transport";
                yield return "tcp";
                yield return "-i";
                yield return _source.StreamUrl;
            }

            yield return "-an";
            yield return "-frames:v";
            yield return "1";
            yield return "-f";
            yield return "image2pipe";
            yield return "-vcodec";
            yield return "mjpeg";
            yield return "-q:v";
            yield return "2";
            yield return "pipe:1";
        }
    }
}

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
        private const int FfmpegWebcamWarmupFrames = 8;
        private readonly CaptureSourceDescriptor _source;
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _mutex = new(1, 1);
        private VideoCapture? _capture;
        private bool _preferFfmpeg;
        private Process? _ffmpegProcess;
        private Stream? _ffmpegStdout;
        private Task<string>? _ffmpegStderrTask;
        private byte[] _ffmpegPendingBytes = [];
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
            DisposeFfmpegProcess();
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

            var frameBytes = await CaptureWithPersistentFfmpegAsync(cancellationToken);

            _cachedFfmpegFrame = frameBytes;
            _cachedFfmpegFrameCapturedAtUtc = now;
            return new MemoryStream(frameBytes, writable: false);
        }

        private async Task<byte[]> CaptureWithPersistentFfmpegAsync(CancellationToken cancellationToken)
        {
            try
            {
                await EnsureFfmpegProcessStartedAsync(cancellationToken);
                return await ReadNextJpegFrameAsync(cancellationToken);
            }
            catch
            {
                DisposeFfmpegProcess();
                await EnsureFfmpegProcessStartedAsync(cancellationToken);
                return await ReadNextJpegFrameAsync(cancellationToken);
            }
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

        private async Task EnsureFfmpegProcessStartedAsync(CancellationToken cancellationToken)
        {
            if (_ffmpegProcess is { HasExited: false } && _ffmpegStdout is not null)
            {
                return;
            }

            DisposeFfmpegProcess();

            _ffmpegProcess = StartFfmpegProcess();
            _ffmpegProcess.Start();
            _ffmpegStdout = _ffmpegProcess.StandardOutput.BaseStream;
            _ffmpegStderrTask = _ffmpegProcess.StandardError.ReadToEndAsync(cancellationToken);
            _ffmpegPendingBytes = [];

            if (_source.Kind == CaptureSourceKind.Webcam)
            {
                for (var index = 0; index < FfmpegWebcamWarmupFrames; index++)
                {
                    await ReadNextJpegFrameAsync(cancellationToken);
                }
            }
        }

        private async Task<byte[]> ReadNextJpegFrameAsync(CancellationToken cancellationToken)
        {
            if (_ffmpegStdout is null)
            {
                throw new InvalidOperationException($"ffmpeg stream is not available for '{_source.DisplayValue}'.");
            }

            while (true)
            {
                if (TryExtractJpegFrame(ref _ffmpegPendingBytes, out var frame))
                {
                    return frame;
                }

                var chunk = new byte[4096];
                var read = await _ffmpegStdout.ReadAsync(chunk, cancellationToken);
                if (read <= 0)
                {
                    var stderr = _ffmpegStderrTask is null ? string.Empty : await _ffmpegStderrTask;
                    var exitCode = _ffmpegProcess?.HasExited == true ? _ffmpegProcess.ExitCode : -1;
                    throw new InvalidOperationException(
                        $"ffmpeg failed to stream frames from '{_source.DisplayValue}' (exit code {exitCode}). {stderr}".Trim());
                }

                var combined = new byte[_ffmpegPendingBytes.Length + read];
                Buffer.BlockCopy(_ffmpegPendingBytes, 0, combined, 0, _ffmpegPendingBytes.Length);
                Buffer.BlockCopy(chunk, 0, combined, _ffmpegPendingBytes.Length, read);
                _ffmpegPendingBytes = combined;
            }
        }

        private void DisposeFfmpegProcess()
        {
            _ffmpegStdout?.Dispose();
            _ffmpegStdout = null;
            _ffmpegPendingBytes = [];

            if (_ffmpegProcess is not null)
            {
                try
                {
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup.
                }
                finally
                {
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
            }
        }

        private static bool TryExtractJpegFrame(ref byte[] buffer, out byte[] frame)
        {
            frame = [];
            if (buffer.Length < 4)
            {
                return false;
            }

            var start = -1;
            for (var index = 0; index < buffer.Length - 1; index++)
            {
                if (buffer[index] == 0xFF && buffer[index + 1] == 0xD8)
                {
                    start = index;
                    break;
                }
            }

            if (start < 0)
            {
                buffer = buffer[^1..];
                return false;
            }

            for (var index = start + 2; index < buffer.Length - 1; index++)
            {
                if (buffer[index] == 0xFF && buffer[index + 1] == 0xD9)
                {
                    var end = index + 1;
                    var length = end - start + 1;
                    frame = new byte[length];
                    Buffer.BlockCopy(buffer, start, frame, 0, length);

                    var remaining = buffer.Length - (end + 1);
                    if (remaining > 0)
                    {
                        var leftover = new byte[remaining];
                        Buffer.BlockCopy(buffer, end + 1, leftover, 0, remaining);
                        buffer = leftover;
                    }
                    else
                    {
                        buffer = [];
                    }

                    return true;
                }
            }

            if (start > 0)
            {
                var trimmed = new byte[buffer.Length - start];
                Buffer.BlockCopy(buffer, start, trimmed, 0, trimmed.Length);
                buffer = trimmed;
            }

            return false;
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
            yield return "-fps_mode";
            yield return "vfr";
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

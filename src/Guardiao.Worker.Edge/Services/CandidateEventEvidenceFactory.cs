using Guardiao.Application.Ports.Outbound;
using Guardiao.Worker.Edge.Options;
using Microsoft.Extensions.Options;
using OpenCvSharp;

namespace Guardiao.Worker.Edge.Services;

public sealed class CandidateEventEvidenceFactory
{
    private readonly EdgeWorkerOptions _options;
    private readonly EdgeMetricsCollector _metrics;
    private readonly ILogger<CandidateEventEvidenceFactory> _logger;

    public CandidateEventEvidenceFactory(
        IOptions<EdgeWorkerOptions> options,
        EdgeMetricsCollector metrics,
        ILogger<CandidateEventEvidenceFactory> logger)
    {
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public IReadOnlyCollection<CandidateEventEvidencePayload> Create(Guid cameraId, byte[] frameBytes, byte[] cropBytes)
    {
        var evidences = new List<CandidateEventEvidencePayload>(capacity: 2);

        TryAddSnapshot(evidences, cameraId, frameBytes);
        TryAddCrop(evidences, cameraId, cropBytes);

        return evidences;
    }

    private void TryAddSnapshot(List<CandidateEventEvidencePayload> evidences, Guid cameraId, byte[] frameBytes)
    {
        try
        {
            using var source = Cv2.ImDecode(frameBytes, ImreadModes.Color);
            if (source.Empty())
            {
                return;
            }

            using var resized = ResizeIfNeeded(source);
            var quality = new[] { (int)ImwriteFlags.JpegQuality, _options.EvidenceJpegQuality };
            Cv2.ImEncode(".jpg", resized, out var snapshotBytes, quality);
            evidences.Add(new CandidateEventEvidencePayload("Snapshot", "snapshot.jpg", "image/jpeg", snapshotBytes));
            _metrics.IncrementCounter("evidences_created_total", ("artifact", "Snapshot"), ("camera", cameraId.ToString()));
            _metrics.AddCounter("evidence_bytes_uploaded_total", snapshotBytes.LongLength, ("artifact", "Snapshot"), ("camera", cameraId.ToString()));
        }
        catch (Exception ex)
        {
            _metrics.IncrementCounter("evidence_upload_failures_total", ("artifact", "Snapshot"), ("camera", cameraId.ToString()));
            _logger.LogWarning(ex, "Failed to generate snapshot evidence for camera {CameraId}.", cameraId);
        }
    }

    private void TryAddCrop(List<CandidateEventEvidencePayload> evidences, Guid cameraId, byte[] cropBytes)
    {
        try
        {
            using var source = Cv2.ImDecode(cropBytes, ImreadModes.Color);
            if (source.Empty())
            {
                return;
            }

            var quality = new[] { (int)ImwriteFlags.JpegQuality, _options.EvidenceJpegQuality };
            Cv2.ImEncode(".jpg", source, out var encodedCropBytes, quality);
            evidences.Add(new CandidateEventEvidencePayload("FaceCrop", "face-crop.jpg", "image/jpeg", encodedCropBytes));
            _metrics.IncrementCounter("evidences_created_total", ("artifact", "FaceCrop"), ("camera", cameraId.ToString()));
            _metrics.AddCounter("evidence_bytes_uploaded_total", encodedCropBytes.LongLength, ("artifact", "FaceCrop"), ("camera", cameraId.ToString()));
        }
        catch (Exception ex)
        {
            _metrics.IncrementCounter("evidence_upload_failures_total", ("artifact", "FaceCrop"), ("camera", cameraId.ToString()));
            _logger.LogWarning(ex, "Failed to generate face crop evidence for camera {CameraId}.", cameraId);
        }
    }

    private Mat ResizeIfNeeded(Mat source)
    {
        if (source.Width <= _options.EvidenceSnapshotMaxWidthPixels)
        {
            return source.Clone();
        }

        var ratio = (double)_options.EvidenceSnapshotMaxWidthPixels / source.Width;
        var targetHeight = Math.Max(1, (int)Math.Round(source.Height * ratio));
        var resized = new Mat();
        Cv2.Resize(source, resized, new Size(_options.EvidenceSnapshotMaxWidthPixels, targetHeight), 0, 0, InterpolationFlags.Area);
        return resized;
    }
}

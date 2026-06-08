using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Options;

public sealed class EdgeWorkerOptions
{
    public const string SectionName = "EdgeWorker";

    public int HealthPort { get; set; } = 18081;
    public int QueueSizePerCamera { get; set; } = 2;
    public int IngressTargetFps { get; set; } = 15;
    public int ProcessingTargetFps { get; set; } = 4;
    public int ReconnectDelayMilliseconds { get; set; } = 250;
    public double MatchThreshold { get; set; } = 0.82;
    public double MinimumDetectionScore { get; set; } = 0.60;
    public string DetectionModelPath { get; set; } = "models/haarcascade_frontalface_default.xml";
    public double DetectionScaleFactor { get; set; } = 1.1;
    public int DetectionMinNeighbors { get; set; } = 4;
    public int DetectionMinFaceSizePixels { get; set; } = 48;
    public string EmbeddingModelPath { get; set; } = "models/face-embedding.onnx";
    public string EmbeddingInputName { get; set; } = string.Empty;
    public string EmbeddingOutputName { get; set; } = string.Empty;
    public int EmbeddingInputWidth { get; set; } = 112;
    public int EmbeddingInputHeight { get; set; } = 112;
    public double EmbeddingPixelMean { get; set; } = 127.5;
    public double EmbeddingPixelStdDev { get; set; } = 128.0;
    public int OnnxIntraOpThreads { get; set; } = 1;
    public int OnnxInterOpThreads { get; set; } = 1;
    public string ApiBaseUrl { get; set; } = "http://localhost:8080";
    public string ApiSharedSecret { get; set; } = string.Empty;
    public string WorkerId { get; set; } = "edge-worker";
    public int PublishTimeoutSeconds { get; set; } = 10;
    public int PublishRetryAttempts { get; set; } = 3;
    public int PublishInitialRetryDelayMilliseconds { get; set; } = 250;
    public int GalleryRefreshIntervalSeconds { get; set; } = 30;
    public int EvidenceSnapshotMaxWidthPixels { get; set; } = 640;
    public int EvidenceJpegQuality { get; set; } = 85;
    public List<EdgeCameraOptions> Cameras { get; set; } = [];
    public List<RestrictedGallerySeedOptions> RestrictedGallery { get; set; } = [];
}

public sealed class EdgeCameraOptions
{
    public Guid CameraId { get; set; }
    public Guid SiteId { get; set; }
    public Guid ProtectedCaseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class RestrictedGallerySeedOptions
{
    public Guid ProtectedCaseId { get; set; }
    public Guid SiteId { get; set; }
    public Guid PersonProjectionId { get; set; }
    public string ExternalPersonId { get; set; } = string.Empty;
    public bool IsBystander { get; set; }
    public float[] Embedding { get; set; } = [];
}

public sealed class EdgeWorkerOptionsValidator : IValidateOptions<EdgeWorkerOptions>
{
    public ValidateOptionsResult Validate(string? name, EdgeWorkerOptions options)
    {
        var errors = new List<string>();

        if (options.HealthPort <= 0)
        {
            errors.Add("EdgeWorker:HealthPort must be greater than zero.");
        }
        else if (options.HealthPort > 65535)
        {
            errors.Add("EdgeWorker:HealthPort must be lower than 65536.");
        }

        if (options.QueueSizePerCamera <= 0)
        {
            errors.Add("EdgeWorker:QueueSizePerCamera must be greater than zero.");
        }

        if (options.IngressTargetFps <= 0)
        {
            errors.Add("EdgeWorker:IngressTargetFps must be greater than zero.");
        }

        if (options.ProcessingTargetFps <= 0)
        {
            errors.Add("EdgeWorker:ProcessingTargetFps must be greater than zero.");
        }

        if (options.MatchThreshold is < 0 or > 1)
        {
            errors.Add("EdgeWorker:MatchThreshold must be between 0 and 1.");
        }

        if (options.MinimumDetectionScore is < 0 or > 1)
        {
            errors.Add("EdgeWorker:MinimumDetectionScore must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(options.DetectionModelPath))
        {
            errors.Add("EdgeWorker:DetectionModelPath is required.");
        }
        else if (!File.Exists(options.DetectionModelPath))
        {
            errors.Add($"EdgeWorker:DetectionModelPath was not found at '{options.DetectionModelPath}'.");
        }

        if (options.DetectionScaleFactor <= 1.0)
        {
            errors.Add("EdgeWorker:DetectionScaleFactor must be greater than 1.");
        }

        if (options.DetectionMinNeighbors < 0)
        {
            errors.Add("EdgeWorker:DetectionMinNeighbors must be zero or greater.");
        }

        if (options.DetectionMinFaceSizePixels <= 0)
        {
            errors.Add("EdgeWorker:DetectionMinFaceSizePixels must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.EmbeddingModelPath))
        {
            errors.Add("EdgeWorker:EmbeddingModelPath is required.");
        }
        else if (!File.Exists(options.EmbeddingModelPath))
        {
            errors.Add($"EdgeWorker:EmbeddingModelPath was not found at '{options.EmbeddingModelPath}'.");
        }

        if (options.EmbeddingInputWidth <= 0)
        {
            errors.Add("EdgeWorker:EmbeddingInputWidth must be greater than zero.");
        }

        if (options.EmbeddingInputHeight <= 0)
        {
            errors.Add("EdgeWorker:EmbeddingInputHeight must be greater than zero.");
        }

        if (options.EmbeddingPixelStdDev <= 0)
        {
            errors.Add("EdgeWorker:EmbeddingPixelStdDev must be greater than zero.");
        }

        if (options.OnnxIntraOpThreads <= 0)
        {
            errors.Add("EdgeWorker:OnnxIntraOpThreads must be greater than zero.");
        }

        if (options.OnnxInterOpThreads <= 0)
        {
            errors.Add("EdgeWorker:OnnxInterOpThreads must be greater than zero.");
        }

        if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("EdgeWorker:ApiBaseUrl must be an absolute http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiSharedSecret))
        {
            errors.Add("EdgeWorker:ApiSharedSecret is required.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerId))
        {
            errors.Add("EdgeWorker:WorkerId is required.");
        }

        if (options.PublishTimeoutSeconds <= 0)
        {
            errors.Add("EdgeWorker:PublishTimeoutSeconds must be greater than zero.");
        }

        if (options.PublishRetryAttempts <= 0)
        {
            errors.Add("EdgeWorker:PublishRetryAttempts must be greater than zero.");
        }

        if (options.PublishInitialRetryDelayMilliseconds <= 0)
        {
            errors.Add("EdgeWorker:PublishInitialRetryDelayMilliseconds must be greater than zero.");
        }

        if (options.GalleryRefreshIntervalSeconds <= 0)
        {
            errors.Add("EdgeWorker:GalleryRefreshIntervalSeconds must be greater than zero.");
        }

        if (options.EvidenceSnapshotMaxWidthPixels <= 0)
        {
            errors.Add("EdgeWorker:EvidenceSnapshotMaxWidthPixels must be greater than zero.");
        }

        if (options.EvidenceJpegQuality is < 1 or > 100)
        {
            errors.Add("EdgeWorker:EvidenceJpegQuality must be between 1 and 100.");
        }

        foreach (var camera in options.Cameras)
        {
            if (camera.CameraId == Guid.Empty)
            {
                errors.Add("EdgeWorker:Cameras:CameraId is required.");
            }

            if (camera.SiteId == Guid.Empty)
            {
                errors.Add("EdgeWorker:Cameras:SiteId is required.");
            }

            if (camera.ProtectedCaseId == Guid.Empty)
            {
                errors.Add("EdgeWorker:Cameras:ProtectedCaseId is required.");
            }

            if (string.IsNullOrWhiteSpace(camera.Source))
            {
                errors.Add("EdgeWorker:Cameras:Source is required.");
            }
            else if (!camera.Source.StartsWith("webcam://", StringComparison.OrdinalIgnoreCase) &&
                     !camera.Source.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("EdgeWorker:Cameras:Source must use webcam:// or rtsp://.");
            }
        }

        foreach (var entry in options.RestrictedGallery)
        {
            if (entry.ProtectedCaseId == Guid.Empty)
            {
                errors.Add("EdgeWorker:RestrictedGallery:ProtectedCaseId is required.");
            }

            if (entry.SiteId == Guid.Empty)
            {
                errors.Add("EdgeWorker:RestrictedGallery:SiteId is required.");
            }

            if (entry.PersonProjectionId == Guid.Empty)
            {
                errors.Add("EdgeWorker:RestrictedGallery:PersonProjectionId is required.");
            }

            if (string.IsNullOrWhiteSpace(entry.ExternalPersonId))
            {
                errors.Add("EdgeWorker:RestrictedGallery:ExternalPersonId is required.");
            }
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

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

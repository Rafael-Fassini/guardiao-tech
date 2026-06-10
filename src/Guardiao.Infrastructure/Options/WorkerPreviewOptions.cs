using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class WorkerPreviewOptions
{
    public const string SectionName = "WorkerPreview";

    public string BaseUrl { get; set; } = "http://localhost:18081";
    public int RequestTimeoutSeconds { get; set; } = 3;
}

public sealed class WorkerPreviewOptionsValidator : IValidateOptions<WorkerPreviewOptions>
{
    public ValidateOptionsResult Validate(string? name, WorkerPreviewOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("WorkerPreview:BaseUrl must be an absolute http or https URL.");
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            errors.Add("WorkerPreview:RequestTimeoutSeconds must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

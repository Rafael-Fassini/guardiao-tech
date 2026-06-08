using Microsoft.Extensions.Options;

namespace Guardiao.Web.Services;

public sealed class OperationsPanelOptions
{
    public const string SectionName = "PanelApi";

    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string SharedSecret { get; set; } = string.Empty;
}

public sealed class OperationsPanelOptionsValidator : IValidateOptions<OperationsPanelOptions>
{
    public ValidateOptionsResult Validate(string? name, OperationsPanelOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("PanelApi:BaseUrl must be an absolute http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(options.SharedSecret))
        {
            errors.Add("PanelApi:SharedSecret must be configured.");
        }
        else if (options.SharedSecret.Trim().Length < 12)
        {
            errors.Add("PanelApi:SharedSecret must be at least 12 characters.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

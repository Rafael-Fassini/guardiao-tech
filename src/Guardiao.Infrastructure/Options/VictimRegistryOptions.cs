using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public class VictimRegistryOptions
{
    public const string SectionName = "VictimRegistry";

    public string BaseUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string StaticAccessToken { get; set; } = string.Empty;
    public int AllowedClockSkewSeconds { get; set; } = 300;
    public int ReconciliationIntervalSeconds { get; set; } = 300;
    public int ReconciliationPageSize { get; set; } = 100;
    public int InitialLookbackMinutes { get; set; } = 60;
}

public sealed class VictimRegistryOptionsValidator : IValidateOptions<VictimRegistryOptions>
{
    private readonly IHostEnvironment _environment;

    public VictimRegistryOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, VictimRegistryOptions options)
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            errors.Add("VictimRegistry:BaseUrl must be an absolute URL.");
        }
        else if (baseUri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add("VictimRegistry:BaseUrl must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(options.StaticAccessToken))
        {
            if (!Uri.TryCreate(options.TokenUrl, UriKind.Absolute, out var tokenUri))
            {
                errors.Add("VictimRegistry:TokenUrl must be an absolute URL when StaticAccessToken is not provided.");
            }
            else if (tokenUri.Scheme != Uri.UriSchemeHttps)
            {
                errors.Add("VictimRegistry:TokenUrl must use HTTPS.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                errors.Add("VictimRegistry:ClientId is required.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                errors.Add("VictimRegistry:ClientSecret is required.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            errors.Add("VictimRegistry:WebhookSecret is required.");
        }

        if (!_environment.IsDevelopment())
        {
            if (string.Equals(options.ClientSecret, "change-me", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("VictimRegistry:ClientSecret must not use a placeholder value outside Development.");
            }

            if (string.Equals(options.WebhookSecret, "change-me-too", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("VictimRegistry:WebhookSecret must not use a placeholder value outside Development.");
            }
        }

        if (options.AllowedClockSkewSeconds <= 0)
        {
            errors.Add("VictimRegistry:AllowedClockSkewSeconds must be greater than zero.");
        }

        if (options.ReconciliationIntervalSeconds <= 0)
        {
            errors.Add("VictimRegistry:ReconciliationIntervalSeconds must be greater than zero.");
        }

        if (options.ReconciliationPageSize <= 0)
        {
            errors.Add("VictimRegistry:ReconciliationPageSize must be greater than zero.");
        }

        if (options.InitialLookbackMinutes <= 0)
        {
            errors.Add("VictimRegistry:InitialLookbackMinutes must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

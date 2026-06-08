using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    public bool EnableDebugHeaderAuthentication { get; set; }
    public string PanelSharedSecret { get; set; } = string.Empty;
    public string WorkerSharedSecret { get; set; } = string.Empty;
    public bool EnableSwaggerUi { get; set; }
    public long MaxApiRequestBodyBytes { get; set; } = 1024 * 1024;
    public long MaxWebhookRequestBodyBytes { get; set; } = 16 * 1024;
    public int ApiWriteRateLimitPermitLimit { get; set; } = 60;
    public int ApiWriteRateLimitWindowSeconds { get; set; } = 60;
    public int WebhookRateLimitPermitLimit { get; set; } = 30;
    public int WebhookRateLimitWindowSeconds { get; set; } = 60;
}

public sealed class ApiSecurityOptionsValidator : IValidateOptions<ApiSecurityOptions>
{
    private readonly IHostEnvironment _environment;

    public ApiSecurityOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, ApiSecurityOptions options)
    {
        var errors = new List<string>();

        if (options.MaxApiRequestBodyBytes <= 0)
        {
            errors.Add("ApiSecurity:MaxApiRequestBodyBytes must be greater than zero.");
        }
        else if (options.MaxApiRequestBodyBytes < 256 * 1024)
        {
            errors.Add("ApiSecurity:MaxApiRequestBodyBytes must be at least 262144 bytes for pilot incident evidence payloads.");
        }

        if (options.MaxWebhookRequestBodyBytes <= 0)
        {
            errors.Add("ApiSecurity:MaxWebhookRequestBodyBytes must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.PanelSharedSecret))
        {
            errors.Add("ApiSecurity:PanelSharedSecret must be configured.");
        }
        else if (!_environment.IsDevelopment() && options.PanelSharedSecret.Trim().Length < 12)
        {
            errors.Add("ApiSecurity:PanelSharedSecret must be at least 12 characters outside Development.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkerSharedSecret))
        {
            errors.Add("ApiSecurity:WorkerSharedSecret must be configured.");
        }
        else if (!_environment.IsDevelopment() && options.WorkerSharedSecret.Trim().Length < 12)
        {
            errors.Add("ApiSecurity:WorkerSharedSecret must be at least 12 characters outside Development.");
        }

        if (options.ApiWriteRateLimitPermitLimit <= 0)
        {
            errors.Add("ApiSecurity:ApiWriteRateLimitPermitLimit must be greater than zero.");
        }

        if (options.ApiWriteRateLimitWindowSeconds <= 0)
        {
            errors.Add("ApiSecurity:ApiWriteRateLimitWindowSeconds must be greater than zero.");
        }

        if (options.WebhookRateLimitPermitLimit <= 0)
        {
            errors.Add("ApiSecurity:WebhookRateLimitPermitLimit must be greater than zero.");
        }

        if (options.WebhookRateLimitWindowSeconds <= 0)
        {
            errors.Add("ApiSecurity:WebhookRateLimitWindowSeconds must be greater than zero.");
        }

        if (!_environment.IsDevelopment() && options.EnableDebugHeaderAuthentication)
        {
            errors.Add("ApiSecurity:EnableDebugHeaderAuthentication cannot be enabled outside Development.");
        }

        if (!_environment.IsDevelopment() && options.EnableSwaggerUi)
        {
            errors.Add("ApiSecurity:EnableSwaggerUi cannot be enabled outside Development.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

public sealed class WebSecurityOptions
{
    public const string SectionName = "WebSecurity";

    public bool EnableOperationsDemoLogin { get; set; }
}

public sealed class WebSecurityOptionsValidator : IValidateOptions<WebSecurityOptions>
{
    private readonly IHostEnvironment _environment;

    public WebSecurityOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, WebSecurityOptions options)
    {
        if (!_environment.IsDevelopment() && options.EnableOperationsDemoLogin)
        {
            return ValidateOptionsResult.Fail("WebSecurity:EnableOperationsDemoLogin cannot be enabled outside Development.");
        }

        return ValidateOptionsResult.Success;
    }
}

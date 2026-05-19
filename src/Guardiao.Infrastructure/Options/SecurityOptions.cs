using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class ApiSecurityOptions
{
    public const string SectionName = "ApiSecurity";

    public bool EnableDebugHeaderAuthentication { get; set; }
    public bool EnableSwaggerUi { get; set; }
    public long MaxApiRequestBodyBytes { get; set; } = 64 * 1024;
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

        if (options.MaxWebhookRequestBodyBytes <= 0)
        {
            errors.Add("ApiSecurity:MaxWebhookRequestBodyBytes must be greater than zero.");
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

using Guardiao.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Guardiao.Api.Infrastructure;

public sealed class RequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiSecurityOptions _options;

    public RequestBodyLimitMiddleware(RequestDelegate next, IOptions<ApiSecurityOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task Invoke(HttpContext context)
    {
        var limit = ResolveLimit(context.Request.Path);
        if (limit is not null && context.Request.ContentLength is long contentLength && contentLength > limit.Value)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Request rejected.",
                Detail = "The request body exceeded the configured limit.",
                Instance = context.Request.Path
            });
            return;
        }

        await _next(context);
    }

    private long? ResolveLimit(PathString path)
    {
        if (path.StartsWithSegments("/integrations/victim-registry/webhooks", StringComparison.OrdinalIgnoreCase))
        {
            return _options.MaxWebhookRequestBodyBytes;
        }

        if (path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return _options.MaxApiRequestBodyBytes;
        }

        return null;
    }
}

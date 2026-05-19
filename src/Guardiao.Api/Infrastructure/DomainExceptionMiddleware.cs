using Guardiao.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Infrastructure;

public sealed class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ForbiddenStateTransitionException ex)
        {
            _logger.LogWarning(ex, "Forbidden state transition.");
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "Forbidden state transition.", ex.Message);
        }
        catch (InvariantViolationException ex)
        {
            _logger.LogWarning(ex, "Domain invariant violation.");
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Domain validation failed.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Argument validation failed.");
            await WriteProblemAsync(context, StatusCodes.Status400BadRequest, "Validation failed.", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled request failure. CorrelationId={CorrelationId}", context.TraceIdentifier);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "Request failed.", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problem);
    }
}

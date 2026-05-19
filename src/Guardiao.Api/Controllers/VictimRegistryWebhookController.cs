using Guardiao.Application.DTOs;
using Guardiao.Application.Services;
using Guardiao.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("integrations/victim-registry/webhooks")]
[AllowAnonymous]
[EnableRateLimiting(SecurityRateLimitPolicies.Webhook)]
public class VictimRegistryWebhookController : ControllerBase
{
    private readonly VictimRegistryWebhookService _webhookService;

    public VictimRegistryWebhookController(VictimRegistryWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    [HttpPost]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        if (!Guid.TryParse(Request.Headers["X-Delivery-Id"], out var deliveryId))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Webhook request rejected.");
        }

        if (!DateTimeOffset.TryParse(Request.Headers["X-Event-Timestamp"], out var eventTimestamp))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Webhook request rejected.");
        }

        var eventType = Request.Headers["X-Event-Type"].ToString();
        var signature = Request.Headers["X-Signature-SHA256"].ToString();
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(signature))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Webhook request rejected.");
        }

        try
        {
            var result = await _webhookService.AcceptAsync(
                new VictimRegistryWebhookHeaders(deliveryId, eventType, eventTimestamp, signature),
                rawPayload,
                cancellationToken);

            return Accepted(new
            {
                deliveryId = result.DeliveryId,
                externalCaseId = result.ExternalCaseId
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Webhook request rejected.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already processed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("allowed skew", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Webhook request could not be accepted.");
        }
        catch (InvalidOperationException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Webhook request rejected.");
        }
    }
}

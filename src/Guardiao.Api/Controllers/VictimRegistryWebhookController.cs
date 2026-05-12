using Guardiao.Application.DTOs;
using Guardiao.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("integrations/victim-registry/webhooks")]
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
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid delivery id.");
        }

        if (!DateTimeOffset.TryParse(Request.Headers["X-Event-Timestamp"], out var eventTimestamp))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid event timestamp.");
        }

        var eventType = Request.Headers["X-Event-Type"].ToString();
        var signature = Request.Headers["X-Signature-SHA256"].ToString();
        if (string.IsNullOrWhiteSpace(eventType) || string.IsNullOrWhiteSpace(signature))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing webhook headers.");
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
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: ex.Message);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("already processed", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("allowed skew", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }
}

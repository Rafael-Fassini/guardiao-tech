using System.Text.Json;
using Guardiao.Application.DTOs;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.ValueObjects;

namespace Guardiao.Application.Services;

public class VictimRegistryWebhookService
{
    private readonly IWebhookSignatureVerifier _signatureVerifier;
    private readonly IWebhookDeliveryRepository _webhookDeliveryRepository;
    private readonly IVictimRegistrySyncQueue _syncQueue;
    private readonly IClock _clock;
    private readonly TimeSpan _allowedClockSkew;

    public VictimRegistryWebhookService(
        IWebhookSignatureVerifier signatureVerifier,
        IWebhookDeliveryRepository webhookDeliveryRepository,
        IVictimRegistrySyncQueue syncQueue,
        IClock clock,
        TimeSpan allowedClockSkew)
    {
        _signatureVerifier = signatureVerifier;
        _webhookDeliveryRepository = webhookDeliveryRepository;
        _syncQueue = syncQueue;
        _clock = clock;
        _allowedClockSkew = allowedClockSkew;
    }

    public async Task<WebhookAcceptanceResult> AcceptAsync(
        VictimRegistryWebhookHeaders headers,
        string rawPayload,
        CancellationToken cancellationToken = default)
    {
        if (!_signatureVerifier.IsValid(rawPayload, headers.SignatureSha256, headers.EventTimestamp))
        {
            throw new InvalidOperationException("Invalid webhook signature.");
        }

        var now = _clock.UtcNow;
        if (headers.EventTimestamp.UtcDateTime < now.Subtract(_allowedClockSkew) ||
            headers.EventTimestamp.UtcDateTime > now.Add(_allowedClockSkew))
        {
            throw new InvalidOperationException("Webhook timestamp is outside the allowed skew.");
        }

        var registered = await _webhookDeliveryRepository.TryRegisterAsync(
            headers.DeliveryId,
            headers.EventType,
            now,
            cancellationToken);

        if (!registered)
        {
            throw new InvalidOperationException("Webhook delivery was already processed.");
        }

        var payload = JsonSerializer.Deserialize<VictimRegistryWebhookPayload>(rawPayload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Invalid webhook payload.");

        ValidationService.ValidateString(payload.ExternalCaseId, nameof(payload.ExternalCaseId), 200);

        await _syncQueue.EnqueueAsync(new ExternalCaseId(payload.ExternalCaseId), cancellationToken);
        return new WebhookAcceptanceResult(headers.DeliveryId, payload.ExternalCaseId);
    }
}

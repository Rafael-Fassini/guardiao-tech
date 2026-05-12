using System.Text.Json.Serialization;

namespace Guardiao.Application.DTOs;

public sealed class VictimRegistryWebhookPayload
{
    [JsonPropertyName("external_case_id")]
    public string ExternalCaseId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public long Version { get; set; }
}

public sealed record VictimRegistryWebhookHeaders(
    Guid DeliveryId,
    string EventType,
    DateTimeOffset EventTimestamp,
    string SignatureSha256);

public sealed record WebhookAcceptanceResult(Guid DeliveryId, string ExternalCaseId);

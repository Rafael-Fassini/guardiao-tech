using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Notifications;

public sealed class WebhookIncidentNotificationChannel : IIncidentNotificationChannel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly OperationalNotificationsOptions _options;

    public WebhookIncidentNotificationChannel(HttpClient httpClient, IOptions<OperationalNotificationsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string ChannelName => "webhook";

    public bool IsEnabled => _options.EnableWebhook && !string.IsNullOrWhiteSpace(_options.WebhookUrl);

    public async Task DeliverAsync(IncidentNotificationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        var payload = new WebhookIncidentNotificationPayload(
            envelope.EventType,
            envelope.Notification.IncidentId,
            envelope.Notification.ProtectedCaseId,
            envelope.Notification.CandidateEventId,
            envelope.Notification.CreatedAtUtc,
            envelope.Notification.Status,
            envelope.Notification.HasEvidence,
            envelope.Notification.EscalatedAtUtc);
        var serializedPayload = JsonSerializer.Serialize(payload, JsonOptions);
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.WebhookUrl)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.TryAddWithoutValidation("X-Guardiao-Event", envelope.EventType);
        request.Headers.TryAddWithoutValidation("X-Guardiao-Timestamp", DateTimeOffset.UtcNow.ToString("O"));

        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            request.Headers.TryAddWithoutValidation("X-Guardiao-Signature", CreateSignature(serializedPayload, _options.WebhookSecret));
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string CreateSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private sealed record WebhookIncidentNotificationPayload(
        string EventType,
        Guid IncidentId,
        Guid ProtectedCaseId,
        Guid CandidateEventId,
        DateTime CreatedAtUtc,
        string Status,
        bool HasEvidence,
        DateTime? EscalatedAtUtc);
}

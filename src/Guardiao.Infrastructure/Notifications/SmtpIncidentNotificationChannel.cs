using System.Net;
using System.Net.Mail;
using System.Text;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Notifications;

public sealed class SmtpIncidentNotificationChannel : IIncidentNotificationChannel
{
    private readonly OperationalNotificationsOptions _options;

    public SmtpIncidentNotificationChannel(IOptions<OperationalNotificationsOptions> options)
    {
        _options = options.Value;
    }

    public string ChannelName => "smtp";

    public bool IsEnabled => _options.EnableSmtp &&
                             !string.IsNullOrWhiteSpace(_options.SmtpHost) &&
                             !string.IsNullOrWhiteSpace(_options.SmtpSender) &&
                             _options.SmtpRecipients.Any(x => !string.IsNullOrWhiteSpace(x));

    public async Task DeliverAsync(IncidentNotificationEnvelope envelope, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.SmtpSender),
            Subject = $"[Guardiao] {envelope.EventType} - incidente {envelope.Notification.IncidentId}",
            Body = BuildBody(envelope),
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        foreach (var recipient in _options.SmtpRecipients.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            message.To.Add(recipient.Trim());
        }

        using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
        {
            EnableSsl = _options.SmtpUseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(_options.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(_options.SmtpUsername, _options.SmtpPassword);
        }

        await client.SendMailAsync(message).WaitAsync(
            TimeSpan.FromSeconds(_options.DeliveryTimeoutSeconds),
            cancellationToken);
    }

    private static string BuildBody(IncidentNotificationEnvelope envelope)
    {
        var notification = envelope.Notification;
        var builder = new StringBuilder();
        builder.AppendLine($"EventType: {envelope.EventType}");
        builder.AppendLine($"IncidentId: {notification.IncidentId}");
        builder.AppendLine($"ProtectedCaseId: {notification.ProtectedCaseId}");
        builder.AppendLine($"CandidateEventId: {notification.CandidateEventId}");
        builder.AppendLine($"CreatedAtUtc: {notification.CreatedAtUtc:O}");
        builder.AppendLine($"Status: {notification.Status}");
        builder.AppendLine($"HasEvidence: {notification.HasEvidence}");

        if (notification.EscalatedAtUtc is not null)
        {
            builder.AppendLine($"EscalatedAtUtc: {notification.EscalatedAtUtc.Value:O}");
        }

        return builder.ToString();
    }
}

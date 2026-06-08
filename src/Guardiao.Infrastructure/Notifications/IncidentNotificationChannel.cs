using Guardiao.Application.Ports.Outbound;

namespace Guardiao.Infrastructure.Notifications;

public interface IIncidentNotificationChannel
{
    string ChannelName { get; }

    bool IsEnabled { get; }

    Task DeliverAsync(IncidentNotificationEnvelope envelope, CancellationToken cancellationToken = default);
}

public sealed record IncidentNotificationEnvelope(
    string EventType,
    IncidentNotification Notification);

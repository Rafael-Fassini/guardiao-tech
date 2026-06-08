using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Notifications;

public sealed class OperationalNotificationPort : INotificationPort
{
    private readonly IEnumerable<IIncidentNotificationChannel> _channels;
    private readonly GuardiaoDbContext _dbContext;
    private readonly IMetricsPort _metrics;
    private readonly OperationalNotificationsOptions _options;
    private readonly ILogger<OperationalNotificationPort> _logger;

    public OperationalNotificationPort(
        IEnumerable<IIncidentNotificationChannel> channels,
        GuardiaoDbContext dbContext,
        IMetricsPort metrics,
        IOptions<OperationalNotificationsOptions> options,
        ILogger<OperationalNotificationPort> logger)
    {
        _channels = channels;
        _dbContext = dbContext;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    public Task NotifyIncidentCreatedAsync(IncidentNotification notification, CancellationToken cancellationToken = default)
        => NotifyAsync("incident.created", notification, cancellationToken);

    public Task NotifyIncidentEscalatedAsync(IncidentNotification notification, CancellationToken cancellationToken = default)
        => NotifyAsync("incident.escalated", notification, cancellationToken);

    private async Task NotifyAsync(string eventType, IncidentNotification notification, CancellationToken cancellationToken)
    {
        var enabledChannels = _channels.Where(x => x.IsEnabled).ToArray();
        if (enabledChannels.Length == 0)
        {
            _logger.LogInformation(
                "Operational notification skipped because all channels are disabled. EventType={EventType} IncidentId={IncidentId}",
                eventType,
                notification.IncidentId);

            try
            {
                await PersistRecordAsync(
                    notification.IncidentId,
                    eventType,
                    "none",
                    "Skipped",
                    1,
                    notification.HasEvidence,
                    "all_channels_disabled",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Operational notification skip record could not be persisted. EventType={EventType} IncidentId={IncidentId}",
                    eventType,
                    notification.IncidentId);
            }
            return;
        }

        foreach (var channel in enabledChannels)
        {
            try
            {
                await DeliverWithRetriesAsync(channel, eventType, notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Operational notification bookkeeping failed after channel processing. EventType={EventType} Channel={Channel} IncidentId={IncidentId}",
                    eventType,
                    channel.ChannelName,
                    notification.IncidentId);
            }
        }
    }

    private async Task DeliverWithRetriesAsync(
        IIncidentNotificationChannel channel,
        string eventType,
        IncidentNotification notification,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        var delay = TimeSpan.FromMilliseconds(_options.InitialRetryDelayMilliseconds);

        for (var attempt = 1; attempt <= _options.RetryAttempts; attempt++)
        {
            try
            {
                await channel.DeliverAsync(new IncidentNotificationEnvelope(eventType, notification), cancellationToken)
                    .WaitAsync(TimeSpan.FromSeconds(_options.DeliveryTimeoutSeconds), cancellationToken);

                _metrics.IncrementCounter(
                    "notifications_sent_total",
                    ("event", eventType),
                    ("channel", channel.ChannelName));

                await PersistRecordAsync(
                    notification.IncidentId,
                    eventType,
                    channel.ChannelName,
                    "Sent",
                    attempt,
                    notification.HasEvidence,
                    $"status={notification.Status};has_evidence={notification.HasEvidence.ToString().ToLowerInvariant()}",
                    cancellationToken);

                _dbContext.AuditLogs.Add(new AuditLog(
                    AuditActorType.Integration,
                    "incident.notification.sent",
                    nameof(Incident),
                    notification.IncidentId.ToString(),
                    $"event_type={eventType};channel={channel.ChannelName};attempts={attempt};has_evidence={notification.HasEvidence.ToString().ToLowerInvariant()}"));
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Operational notification delivered. EventType={EventType} Channel={Channel} IncidentId={IncidentId} Attempts={Attempts}",
                    eventType,
                    channel.ChannelName,
                    notification.IncidentId,
                    attempt);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;

                if (attempt < _options.RetryAttempts)
                {
                    _metrics.IncrementCounter(
                        "notification_retries_total",
                        ("event", eventType),
                        ("channel", channel.ChannelName));

                    _logger.LogWarning(
                        ex,
                        "Operational notification attempt failed and will be retried. EventType={EventType} Channel={Channel} IncidentId={IncidentId} Attempt={Attempt}",
                        eventType,
                        channel.ChannelName,
                        notification.IncidentId,
                        attempt);

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                        delay = TimeSpan.FromMilliseconds(Math.Max(delay.TotalMilliseconds * 2, 1));
                    }
                }
            }
        }

        _metrics.IncrementCounter(
            "notification_failures_total",
            ("event", eventType),
            ("channel", channel.ChannelName));

        await PersistRecordAsync(
            notification.IncidentId,
            eventType,
            channel.ChannelName,
            "Failed",
            _options.RetryAttempts,
            notification.HasEvidence,
            lastError?.Message ?? "unknown_notification_failure",
            cancellationToken);

        _dbContext.AuditLogs.Add(new AuditLog(
            AuditActorType.Integration,
            "incident.notification.failed",
            nameof(Incident),
            notification.IncidentId.ToString(),
            $"event_type={eventType};channel={channel.ChannelName};attempts={_options.RetryAttempts};error={lastError?.Message ?? "unknown"}"));
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogError(
            lastError,
            "Operational notification failed after retries. EventType={EventType} Channel={Channel} IncidentId={IncidentId}",
            eventType,
            channel.ChannelName,
            notification.IncidentId);
    }

    private async Task PersistRecordAsync(
        Guid incidentId,
        string eventType,
        string channel,
        string deliveryStatus,
        int attemptCount,
        bool hasEvidence,
        string details,
        CancellationToken cancellationToken)
    {
        _dbContext.IncidentNotificationRecords.Add(new IncidentNotificationRecord(
            incidentId,
            eventType,
            channel,
            deliveryStatus,
            attemptCount,
            hasEvidence,
            details));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

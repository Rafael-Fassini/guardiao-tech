using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Notifications;

public sealed class PendingIncidentEscalationService
{
    private readonly GuardiaoDbContext _dbContext;
    private readonly INotificationPort _notificationPort;
    private readonly IClock _clock;
    private readonly IMetricsPort _metrics;
    private readonly OperationalNotificationsOptions _options;
    private readonly ILogger<PendingIncidentEscalationService> _logger;

    public PendingIncidentEscalationService(
        GuardiaoDbContext dbContext,
        INotificationPort notificationPort,
        IClock clock,
        IMetricsPort metrics,
        IOptions<OperationalNotificationsOptions> options,
        ILogger<PendingIncidentEscalationService> logger)
    {
        _dbContext = dbContext;
        _notificationPort = notificationPort;
        _clock = clock;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> EscalatePendingAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var thresholdUtc = nowUtc.AddMinutes(-_options.EscalationWindowMinutes);

        if (!_options.EnableEscalation)
        {
            _logger.LogInformation("Pending incident escalation disabled by configuration.");
            return 0;
        }

        var incidents = await _dbContext.Incidents
            .Where(x => x.Status == IncidentStatus.PendingReview &&
                        x.EscalatedAtUtc == null &&
                        x.CreatedAtUtc <= thresholdUtc)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var incident in incidents)
        {
            incident.MarkPendingReviewEscalated();
            _dbContext.AuditLogs.Add(new AuditLog(
                AuditActorType.System,
                "incident.escalated",
                nameof(Incident),
                incident.Id.ToString(),
                $"status={incident.Status};escalated_at_utc={incident.EscalatedAtUtc:O};sla_minutes={_options.EscalationWindowMinutes}"));
        }

        if (incidents.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var incident in incidents)
        {
            var hasEvidence = await _dbContext.EvidenceArtifacts.AnyAsync(x => x.IncidentId == incident.Id, cancellationToken);
            await _notificationPort.NotifyIncidentEscalatedAsync(new IncidentNotification(
                incident.Id,
                incident.ProtectedCaseId,
                incident.CandidateEventId,
                incident.CreatedAtUtc,
                incident.Status.ToString(),
                hasEvidence,
                incident.EscalatedAtUtc), cancellationToken);
            _metrics.IncrementCounter("incidents_escalated_total");
        }

        if (incidents.Count > 0)
        {
            _logger.LogInformation(
                "Pending incident escalation completed. EscalatedCount={EscalatedCount} ThresholdUtc={ThresholdUtc}",
                incidents.Count,
                thresholdUtc);
        }

        var escalatedPendingCount = await _dbContext.Incidents.CountAsync(
            x => x.Status == IncidentStatus.PendingReview && x.EscalatedAtUtc != null,
            cancellationToken);
        _metrics.RecordGauge("escalated_incidents_pending_total", escalatedPendingCount);

        return incidents.Count;
    }
}

using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class EvidenceEligibilityScanBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HousekeepingOptions _options;
    private readonly ILogger<EvidenceEligibilityScanBackgroundService> _logger;

    public EvidenceEligibilityScanBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<HousekeepingOptions> options,
        ILogger<EvidenceEligibilityScanBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableEvidenceEligibilityScan)
        {
            _logger.LogInformation("Evidence eligibility scan disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.EvidenceEligibilityScanIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ScanOnceAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ScanOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var retention = scope.ServiceProvider.GetRequiredService<IRetentionPolicyProvider>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var metrics = scope.ServiceProvider.GetRequiredService<IMetricsPort>();

            var artifacts = await dbContext.EvidenceArtifacts.ToListAsync(cancellationToken);
            var eligibleCount = artifacts.Count(artifact =>
                artifact.CreatedAtUtc.Add(retention.GetRetentionWindow(artifact.RetentionMode)) <= clock.UtcNow);

            metrics.RecordGauge("evidence_retention_eligible_total", eligibleCount);
            metrics.IncrementCounter("housekeeping_evidence_scan_runs_total");

            _logger.LogInformation(
                "Evidence eligibility scan completed. EligibleArtifacts={EligibleArtifacts} TotalArtifacts={TotalArtifacts}",
                eligibleCount,
                artifacts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evidence eligibility scan failed.");
        }
    }
}

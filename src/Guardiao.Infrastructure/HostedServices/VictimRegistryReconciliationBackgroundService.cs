using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class VictimRegistryReconciliationBackgroundService : BackgroundService
{
    private const string CursorName = "victim-registry-reconciliation";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VictimRegistryOptions _options;
    private readonly ILogger<VictimRegistryReconciliationBackgroundService> _logger;

    public VictimRegistryReconciliationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<VictimRegistryOptions> options,
        ILogger<VictimRegistryReconciliationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.ReconciliationIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await ReconcileOnceAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var victimRegistryPort = scope.ServiceProvider.GetRequiredService<IVictimRegistryPort>();
            var syncService = scope.ServiceProvider.GetRequiredService<VictimRegistrySyncService>();
            var syncCursorRepository = scope.ServiceProvider.GetRequiredService<ISyncCursorRepository>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var cursor = await syncCursorRepository.GetLastCursorAsync(CursorName, cancellationToken)
                ?? clock.UtcNow.AddMinutes(-_options.InitialLookbackMinutes);

            var page = 1;
            DateTime maxCursor = cursor;

            while (!cancellationToken.IsCancellationRequested)
            {
                var cases = await victimRegistryPort.GetCasesUpdatedSinceAsync(cursor, page, _options.ReconciliationPageSize, cancellationToken);
                if (cases.Count == 0)
                {
                    break;
                }

                foreach (var snapshot in cases)
                {
                    await syncService.SyncSnapshotAsync(snapshot, cancellationToken);
                    if (snapshot.UpdatedAtUtc > maxCursor)
                    {
                        maxCursor = snapshot.UpdatedAtUtc;
                    }
                }

                if (cases.Count < _options.ReconciliationPageSize)
                {
                    break;
                }

                page++;
            }

            await syncCursorRepository.SaveLastCursorAsync(CursorName, maxCursor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Victim registry reconciliation failed.");
        }
    }
}

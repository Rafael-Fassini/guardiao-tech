using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class VictimRegistryReconciliationBackgroundService : BackgroundService
{
    private const string CursorName = "victim-registry-reconciliation";

    private readonly IVictimRegistryPort _victimRegistryPort;
    private readonly VictimRegistrySyncService _syncService;
    private readonly ISyncCursorRepository _syncCursorRepository;
    private readonly IClock _clock;
    private readonly VictimRegistryOptions _options;
    private readonly ILogger<VictimRegistryReconciliationBackgroundService> _logger;

    public VictimRegistryReconciliationBackgroundService(
        IVictimRegistryPort victimRegistryPort,
        VictimRegistrySyncService syncService,
        ISyncCursorRepository syncCursorRepository,
        IClock clock,
        IOptions<VictimRegistryOptions> options,
        ILogger<VictimRegistryReconciliationBackgroundService> logger)
    {
        _victimRegistryPort = victimRegistryPort;
        _syncService = syncService;
        _syncCursorRepository = syncCursorRepository;
        _clock = clock;
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
            var cursor = await _syncCursorRepository.GetLastCursorAsync(CursorName, cancellationToken)
                ?? _clock.UtcNow.AddMinutes(-_options.InitialLookbackMinutes);

            var page = 1;
            DateTime maxCursor = cursor;

            while (!cancellationToken.IsCancellationRequested)
            {
                var cases = await _victimRegistryPort.GetCasesUpdatedSinceAsync(cursor, page, _options.ReconciliationPageSize, cancellationToken);
                if (cases.Count == 0)
                {
                    break;
                }

                foreach (var snapshot in cases)
                {
                    await _syncService.SyncSnapshotAsync(snapshot, cancellationToken);
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

            await _syncCursorRepository.SaveLastCursorAsync(CursorName, maxCursor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Victim registry reconciliation failed.");
        }
    }
}

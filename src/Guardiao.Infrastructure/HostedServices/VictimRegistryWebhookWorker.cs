using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class VictimRegistryWebhookWorker : BackgroundService
{
    private readonly IVictimRegistrySyncQueue _syncQueue;
    private readonly VictimRegistrySyncService _syncService;
    private readonly ILogger<VictimRegistryWebhookWorker> _logger;

    public VictimRegistryWebhookWorker(
        IVictimRegistrySyncQueue syncQueue,
        VictimRegistrySyncService syncService,
        ILogger<VictimRegistryWebhookWorker> logger)
    {
        _syncQueue = syncQueue;
        _syncService = syncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var externalCaseId = await _syncQueue.DequeueAsync(stoppingToken);

            try
            {
                await _syncService.SyncCaseAsync(externalCaseId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process victim registry sync for case {ExternalCaseId}", externalCaseId.Value);
            }
        }
    }
}

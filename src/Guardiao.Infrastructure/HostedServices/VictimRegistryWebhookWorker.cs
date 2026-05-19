using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class VictimRegistryWebhookWorker : BackgroundService
{
    private readonly IVictimRegistrySyncQueue _syncQueue;
    private readonly VictimRegistrySyncService _syncService;
    private readonly ILogger<VictimRegistryWebhookWorker> _logger;
    private readonly SensitiveDataRedactor _redactor;

    public VictimRegistryWebhookWorker(
        IVictimRegistrySyncQueue syncQueue,
        VictimRegistrySyncService syncService,
        ILogger<VictimRegistryWebhookWorker> logger,
        SensitiveDataRedactor redactor)
    {
        _syncQueue = syncQueue;
        _syncService = syncService;
        _logger = logger;
        _redactor = redactor;
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
                _logger.LogError(ex, "Failed to process victim registry sync for case {ExternalCaseIdMasked}", _redactor.RedactIdentifier(externalCaseId.Value));
            }
        }
    }
}

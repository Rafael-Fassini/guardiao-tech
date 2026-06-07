using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class VictimRegistryWebhookWorker : BackgroundService
{
    private readonly IVictimRegistrySyncQueue _syncQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VictimRegistryWebhookWorker> _logger;
    private readonly SensitiveDataRedactor _redactor;

    public VictimRegistryWebhookWorker(
        IVictimRegistrySyncQueue syncQueue,
        IServiceScopeFactory scopeFactory,
        ILogger<VictimRegistryWebhookWorker> logger,
        SensitiveDataRedactor redactor)
    {
        _syncQueue = syncQueue;
        _scopeFactory = scopeFactory;
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
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<VictimRegistrySyncService>();
                await syncService.SyncCaseAsync(externalCaseId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process victim registry sync for case {ExternalCaseIdMasked}", _redactor.RedactIdentifier(externalCaseId.Value));
            }
        }
    }
}

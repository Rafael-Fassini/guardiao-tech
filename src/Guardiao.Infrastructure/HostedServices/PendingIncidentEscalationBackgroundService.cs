using Guardiao.Infrastructure.Notifications;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.HostedServices;

public sealed class PendingIncidentEscalationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OperationalNotificationsOptions _options;
    private readonly ILogger<PendingIncidentEscalationBackgroundService> _logger;

    public PendingIncidentEscalationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OperationalNotificationsOptions> options,
        ILogger<PendingIncidentEscalationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.EscalationScanIntervalSeconds));

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
            var service = scope.ServiceProvider.GetRequiredService<PendingIncidentEscalationService>();
            await service.EscalatePendingAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pending incident escalation scan failed.");
        }
    }
}

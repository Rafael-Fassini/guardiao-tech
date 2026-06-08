using Guardiao.Worker.Edge.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public sealed class GalleryRefreshBackgroundService : BackgroundService
{
    private readonly ApiRestrictedGalleryProvider _galleryProvider;
    private readonly EdgeWorkerOptions _options;

    public GalleryRefreshBackgroundService(
        ApiRestrictedGalleryProvider galleryProvider,
        IOptions<EdgeWorkerOptions> options)
    {
        _galleryProvider = galleryProvider;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _galleryProvider.RefreshAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_options.GalleryRefreshIntervalSeconds), stoppingToken);
        }
    }
}

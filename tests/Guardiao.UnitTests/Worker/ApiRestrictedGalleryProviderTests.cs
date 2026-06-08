using System.Net;
using System.Text;
using System.Text.Json;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class ApiRestrictedGalleryProviderTests
{
    [Fact]
    public async Task RefreshAsync_ShouldPopulateCache_FromApiPayload()
    {
        var protectedCaseId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var handler = new GalleryHandler(new[]
        {
            new
            {
                protectedCaseId,
                siteId,
                personProjectionId = Guid.NewGuid(),
                externalPersonId = "person-1",
                isBystander = false,
                embedding = new[] { 0.1f, 0.2f, 0.3f }
            }
        });

        var provider = new ApiRestrictedGalleryProvider(
            new HttpClient(handler) { BaseAddress = new Uri("https://guardiao-api.test") },
            Options.Create(new EdgeWorkerOptions
            {
                ApiSharedSecret = "worker-secret",
                WorkerId = "edge-worker-01",
                Cameras =
                [
                    new EdgeCameraOptions
                    {
                        CameraId = Guid.NewGuid(),
                        SiteId = siteId,
                        ProtectedCaseId = protectedCaseId,
                        Name = "Cam",
                        Source = "webcam://0",
                        Enabled = true
                    }
                ]
            }),
            new EdgeMetricsCollector(),
            new WorkerOperationalState(Options.Create(new EdgeWorkerOptions
            {
                Cameras =
                [
                    new EdgeCameraOptions
                    {
                        CameraId = Guid.NewGuid(),
                        SiteId = siteId,
                        ProtectedCaseId = protectedCaseId,
                        Name = "Cam",
                        Source = "webcam://0",
                        Enabled = true
                    }
                ]
            })),
            NullLogger<ApiRestrictedGalleryProvider>.Instance);

        await provider.RefreshAsync();

        var cached = provider.GetByScope(protectedCaseId, siteId);
        Assert.Single(cached);
    }

    private sealed class GalleryHandler : HttpMessageHandler
    {
        private readonly object _payload;

        public GalleryHandler(object payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(_payload), Encoding.UTF8, "application/json")
            });
        }
    }
}

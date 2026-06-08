using System.Collections.Concurrent;
using System.Net.Http.Json;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public sealed class ApiRestrictedGalleryProvider : IRestrictedGalleryProvider
{
    private readonly HttpClient _httpClient;
    private readonly EdgeWorkerOptions _options;
    private readonly EdgeMetricsCollector _metrics;
    private readonly ILogger<ApiRestrictedGalleryProvider> _logger;
    private readonly ConcurrentDictionary<(Guid ProtectedCaseId, Guid SiteId), IReadOnlyCollection<GalleryCandidate>> _cache = new();

    public ApiRestrictedGalleryProvider(
        HttpClient httpClient,
        IOptions<EdgeWorkerOptions> options,
        EdgeMetricsCollector metrics,
        ILogger<ApiRestrictedGalleryProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public IReadOnlyCollection<GalleryCandidate> GetByScope(Guid protectedCaseId, Guid siteId)
    {
        return _cache.TryGetValue((protectedCaseId, siteId), out var entries)
            ? entries
            : Array.Empty<GalleryCandidate>();
    }

    public IReadOnlyCollection<GalleryCandidate> GetByProtectedCase(Guid protectedCaseId)
    {
        return _cache
            .Where(x => x.Key.ProtectedCaseId == protectedCaseId)
            .SelectMany(x => x.Value)
            .ToArray();
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var scopes = _options.Cameras
            .Where(x => x.Enabled)
            .Select(x => (x.ProtectedCaseId, x.SiteId))
            .Distinct()
            .ToArray();

        foreach (var scope in scopes)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/cases/{scope.ProtectedCaseId}/gallery?siteId={scope.SiteId}");
                request.Headers.TryAddWithoutValidation("X-Worker-Id", _options.WorkerId);
                request.Headers.TryAddWithoutValidation("X-Worker-Auth", _options.ApiSharedSecret);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<List<BiometricGalleryEntryPayload>>(cancellationToken: cancellationToken)
                    ?? [];

                var candidates = payload
                    .Select(x => new GalleryCandidate(
                        x.ProtectedCaseId,
                        x.SiteId,
                        x.PersonProjectionId,
                        x.ExternalPersonId,
                        x.IsBystander,
                        x.Embedding))
                    .ToArray();

                _cache[(scope.ProtectedCaseId, scope.SiteId)] = candidates;
                _metrics.IncrementCounter("gallery_refresh_success_total", ("case", scope.ProtectedCaseId.ToString()), ("site", scope.SiteId.ToString()));
                _metrics.RecordGauge("gallery_entries_cached", candidates.Length, ("case", scope.ProtectedCaseId.ToString()), ("site", scope.SiteId.ToString()));
            }
            catch (Exception ex)
            {
                _metrics.IncrementCounter("gallery_refresh_failures_total", ("case", scope.ProtectedCaseId.ToString()), ("site", scope.SiteId.ToString()));
                _logger.LogWarning(ex, "Gallery refresh failed for case {ProtectedCaseId} and site {SiteId}.", scope.ProtectedCaseId, scope.SiteId);
            }
        }
    }

    private sealed record BiometricGalleryEntryPayload(
        Guid ProtectedCaseId,
        Guid SiteId,
        Guid PersonProjectionId,
        string ExternalPersonId,
        bool IsBystander,
        float[] Embedding);
}

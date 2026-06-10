using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Api.Infrastructure;

public interface ICameraLivePreviewPort
{
    Task<CameraLivePreviewPayload?> GetLatestPreviewAsync(Guid cameraId, CancellationToken cancellationToken = default);
    Task<CameraLivePreviewStreamPayload?> OpenLivePreviewStreamAsync(Guid cameraId, CancellationToken cancellationToken = default);
}

public sealed record CameraLivePreviewPayload(
    byte[] Content,
    string ContentType,
    DateTime? CapturedAtUtc,
    long? Sequence);

public sealed class CameraLivePreviewStreamPayload : IAsyncDisposable
{
    private readonly HttpResponseMessage _response;

    public CameraLivePreviewStreamPayload(HttpResponseMessage response, Stream content, string contentType)
    {
        _response = response;
        Content = content;
        ContentType = contentType;
    }

    public Stream Content { get; }
    public string ContentType { get; }

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        _response.Dispose();
    }
}

public sealed class HttpCameraLivePreviewPort : ICameraLivePreviewPort
{
    private readonly HttpClient _httpClient;

    public HttpCameraLivePreviewPort(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CameraLivePreviewPayload?> GetLatestPreviewAsync(Guid cameraId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/cameras/{cameraId}/preview");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        DateTime? capturedAtUtc = null;
        long? sequence = null;

        if (response.Headers.TryGetValues("X-Captured-At-Utc", out var values) &&
            DateTime.TryParse(values.FirstOrDefault(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            capturedAtUtc = parsed;
        }

        if (response.Headers.TryGetValues("X-Frame-Sequence", out var sequenceValues) &&
            long.TryParse(sequenceValues.FirstOrDefault(), out var parsedSequence))
        {
            sequence = parsedSequence;
        }

        return new CameraLivePreviewPayload(bytes, contentType, capturedAtUtc, sequence);
    }

    public async Task<CameraLivePreviewStreamPayload?> OpenLivePreviewStreamAsync(Guid cameraId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/cameras/{cameraId}/live");
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "multipart/x-mixed-replace; boundary=guardiao-frame";
        return new CameraLivePreviewStreamPayload(response, stream, contentType);
    }
}

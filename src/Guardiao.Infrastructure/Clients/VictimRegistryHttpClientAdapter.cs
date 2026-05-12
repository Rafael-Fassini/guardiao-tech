using System.Net.Http.Headers;
using System.Net.Http.Json;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Clients;

public interface IVictimRegistryAccessTokenProvider
{
    Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}

public sealed class VictimRegistryClientCredentialsTokenProvider : IVictimRegistryAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly VictimRegistryOptions _options;

    public VictimRegistryClientCredentialsTokenProvider(HttpClient httpClient, IOptions<VictimRegistryOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_options.StaticAccessToken))
        {
            return _options.StaticAccessToken;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope
            })
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Victim registry token response was empty.");

        return payload.AccessToken;
    }
}

public sealed class VictimRegistryHttpClientAdapter : IVictimRegistryPort, IVictimRegistryMediaPort
{
    private readonly HttpClient _httpClient;
    private readonly IVictimRegistryAccessTokenProvider _tokenProvider;

    public VictimRegistryHttpClientAdapter(HttpClient httpClient, IVictimRegistryAccessTokenProvider tokenProvider)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
    }

    public async Task<VictimRegistryCaseSnapshot?> GetCaseAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{Uri.EscapeDataString(externalCaseId.Value)}");
        await AuthorizeAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<VictimRegistryCaseResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Victim registry case payload was empty.");

        return MapCase(payload);
    }

    public async Task<IReadOnlyCollection<VictimRegistryCaseSnapshot>> GetCasesUpdatedSinceAsync(DateTime updatedSinceUtc, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = $"/api/v1/cases?updated_since={Uri.EscapeDataString(updatedSinceUtc.ToString("O"))}&page={page}&page_size={pageSize}";
        var request = new HttpRequestMessage(HttpMethod.Get, query);
        await AuthorizeAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<VictimRegistryCasesPageResponse>(cancellationToken: cancellationToken)
            ?? new VictimRegistryCasesPageResponse();

        return payload.Items.Select(MapCase).ToArray();
    }

    public async Task<IReadOnlyCollection<VictimRegistryMediaItem>> ListMediaAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{Uri.EscapeDataString(externalCaseId.Value)}/media");
        await AuthorizeAsync(request, cancellationToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<VictimRegistryMediaResponse>>(cancellationToken: cancellationToken)
            ?? [];

        return payload.Select(x => new VictimRegistryMediaItem(x.MediaId, x.ContentType, x.CreatedAtUtc)).ToArray();
    }

    public async Task<Stream> DownloadMediaAsync(ExternalCaseId externalCaseId, string mediaId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cases/{Uri.EscapeDataString(externalCaseId.Value)}/media/{Uri.EscapeDataString(mediaId)}/download");
        await AuthorizeAsync(request, cancellationToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    private async Task AuthorizeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static VictimRegistryCaseSnapshot MapCase(VictimRegistryCaseResponse payload)
    {
        return new VictimRegistryCaseSnapshot(
            new ExternalCaseId(payload.ExternalCaseId),
            new ExternalPersonId(payload.ExternalPersonId),
            payload.Version,
            payload.FullName,
            new MonitoringStatus(payload.MonitoringStatus),
            new ConsentStatus(payload.ConsentStatus),
            payload.IsBystander,
            payload.UpdatedAtUtc);
    }
}

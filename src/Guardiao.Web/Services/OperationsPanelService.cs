using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Authorization;

namespace Guardiao.Web.Services;

public sealed class OperationsPanelService
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public OperationsPanelService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
    {
        _httpClient = httpClient;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<OperationsSummaryModel> GetSummaryAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<OperationsSummaryModel>("/api/operations/summary", cancellationToken);

    public async Task<List<IncidentListItemModel>> ListIncidentsAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<IncidentListItemModel>>("/api/incidents", cancellationToken);

    public async Task<IncidentDetailModel?> GetIncidentAsync(Guid id, CancellationToken cancellationToken = default)
        => await GetOptionalAsync<IncidentDetailModel>($"/api/incidents/{id}", cancellationToken);

    public async Task<List<ProtectedCaseListItemModel>> ListCasesAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<ProtectedCaseListItemModel>>("/api/cases", cancellationToken);

    public async Task<ProtectedCaseDetailModel?> GetCaseAsync(Guid id, CancellationToken cancellationToken = default)
        => await GetOptionalAsync<ProtectedCaseDetailModel>($"/api/cases/{id}", cancellationToken);

    public async Task<List<BiometricTemplateModel>> ListBiometricTemplatesAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<BiometricTemplateModel>>($"/api/cases/{protectedCaseId}/biometrics", cancellationToken);

    public async Task<List<MonitoringRuleModel>> ListRulesAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<MonitoringRuleModel>>($"/api/cases/{protectedCaseId}/rules", cancellationToken);

    public async Task<List<SiteModel>> ListSitesAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<SiteModel>>("/api/sites", cancellationToken);

    public async Task<List<CameraModel>> ListCamerasAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<CameraModel>>("/api/cameras", cancellationToken);

    public async Task<List<AuditEntryModel>> ListAuditAsync(CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<AuditEntryModel>>("/api/audit", cancellationToken);

    public async Task ConfirmIncidentAsync(Guid incidentId, string reviewNotes, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/incidents/{incidentId}/review/confirm",
            new IncidentReviewRequest { ReviewNotes = reviewNotes },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DismissIncidentAsync(Guid incidentId, string reviewNotes, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/incidents/{incidentId}/review/dismiss",
            new IncidentReviewRequest { ReviewNotes = reviewNotes },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateRuleAsync(Guid protectedCaseId, Guid ruleId, Guid siteId, Guid cameraId, TimeOnly startsAt, TimeOnly endsAt, bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/cases/{protectedCaseId}/rules/{ruleId}",
            new UpdateMonitoringRuleRequest
            {
                SiteId = siteId,
                CameraId = cameraId,
                StartsAt = startsAt,
                EndsAt = endsAt,
                IsEnabled = enabled
            },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ToggleCameraAsync(Guid cameraId, bool enabled, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/cameras/{cameraId}/state",
            new UpdateCameraStateRequest { IsEnabled = enabled },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<BiometricTemplateUploadModel> UploadBiometricTemplateAsync(Guid protectedCaseId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();

        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024, cancellationToken);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        form.Add(content, "file", file.Name);

        using var response = await _httpClient.PostAsync($"/api/cases/{protectedCaseId}/biometrics", form, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BiometricTemplateUploadModel>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Response payload was empty for biometric upload.");
    }

    public async Task DeactivateBiometricTemplateAsync(Guid protectedCaseId, Guid templateId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.DeleteAsync($"/api/cases/{protectedCaseId}/biometrics/{templateId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Response payload was empty for '{path}'.");
    }

    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync();
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Operations API call failed with status code {(int)response.StatusCode}: {payload}",
            null,
            response.StatusCode);
    }

    private async Task EnsureAuthenticatedAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("The operations panel requires an authenticated user.");
        }
    }
}

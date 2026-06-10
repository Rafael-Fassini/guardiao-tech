using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
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

    public async Task<List<IncidentEvidenceModel>> ListIncidentEvidencesAsync(Guid incidentId, CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<IncidentEvidenceModel>>($"/api/incidents/{incidentId}/evidences", cancellationToken);

    public async Task<List<IncidentNotificationModel>> ListIncidentNotificationsAsync(Guid incidentId, CancellationToken cancellationToken = default)
        => await GetRequiredAsync<List<IncidentNotificationModel>>($"/api/incidents/{incidentId}/notifications", cancellationToken);

    public async Task<string> GetIncidentEvidenceDataUrlAsync(Guid incidentId, Guid evidenceId, string contentType, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"/api/incidents/{incidentId}/evidences/{evidenceId}/content",
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var effectiveContentType = response.Content.Headers.ContentType?.MediaType ?? contentType;
        return $"data:{effectiveContentType};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task<CameraLivePreviewModel?> GetCameraLivePreviewAsync(Guid cameraId, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"/api/operations/cameras/{cameraId}/preview",
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        DateTime? capturedAtUtc = null;

        if (response.Headers.TryGetValues("X-Captured-At-Utc", out var values) &&
            DateTime.TryParse(values.FirstOrDefault(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            capturedAtUtc = parsed;
        }

        return new CameraLivePreviewModel(
            $"data:{contentType};base64,{Convert.ToBase64String(bytes)}",
            capturedAtUtc);
    }

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
        using var request = await CreateJsonRequestAsync(
            HttpMethod.Post,
            $"/api/incidents/{incidentId}/review/confirm",
            new IncidentReviewRequest { ReviewNotes = reviewNotes },
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DismissIncidentAsync(Guid incidentId, string reviewNotes, CancellationToken cancellationToken = default)
    {
        using var request = await CreateJsonRequestAsync(
            HttpMethod.Post,
            $"/api/incidents/{incidentId}/review/dismiss",
            new IncidentReviewRequest { ReviewNotes = reviewNotes },
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateRuleAsync(Guid protectedCaseId, Guid ruleId, Guid siteId, Guid cameraId, TimeOnly startsAt, TimeOnly endsAt, bool enabled, CancellationToken cancellationToken = default)
    {
        using var request = await CreateJsonRequestAsync(
            HttpMethod.Put,
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
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task UpdateCaseSubjectRoleAsync(Guid protectedCaseId, string subjectRole, CancellationToken cancellationToken = default)
    {
        using var request = await CreateJsonRequestAsync(
            HttpMethod.Put,
            $"/api/cases/{protectedCaseId}/subject-role",
            new UpdateProtectedCaseSubjectRoleRequest { SubjectRole = subjectRole },
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ToggleCameraAsync(Guid cameraId, bool enabled, CancellationToken cancellationToken = default)
    {
        using var request = await CreateJsonRequestAsync(
            HttpMethod.Put,
            $"/api/cameras/{cameraId}/state",
            new UpdateCameraStateRequest { IsEnabled = enabled },
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<BiometricTemplateUploadModel> UploadBiometricTemplateAsync(Guid protectedCaseId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024, cancellationToken);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        form.Add(content, "file", file.Name);

        using var request = await CreateRequestAsync(
            HttpMethod.Post,
            $"/api/cases/{protectedCaseId}/biometrics",
            cancellationToken,
            form);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<BiometricTemplateUploadModel>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Response payload was empty for biometric upload.");
    }

    public async Task DeactivateBiometricTemplateAsync(Guid protectedCaseId, Guid templateId, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Delete,
            $"/api/cases/{protectedCaseId}/biometrics/{templateId}",
            cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException($"Response payload was empty for '{path}'.");
    }

    private async Task<T?> GetOptionalAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
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

    private async Task<HttpRequestMessage> CreateJsonRequestAsync<T>(HttpMethod method, string path, T payload, CancellationToken cancellationToken)
    {
        var request = await CreateRequestAsync(method, path, cancellationToken);
        request.Content = JsonContent.Create(payload);
        return request;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken,
        HttpContent? content = null)
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("The operations panel requires an authenticated user.");
        }

        var userName = state.User.Identity.Name ?? state.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = state.User.FindFirstValue(ClaimTypes.Role) ?? "viewer";
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidOperationException("Authenticated user name is required for operations API calls.");
        }

        var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        request.Headers.TryAddWithoutValidation("X-Panel-User", userName);
        request.Headers.TryAddWithoutValidation("X-Panel-Role", role);
        request.Headers.TryAddWithoutValidation("X-Panel-Auth", ResolveSharedSecret());
        if (!request.Headers.Accept.Any(x => x.MediaType == "application/json"))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        return request;
    }

    private string ResolveSharedSecret()
    {
        if (_httpClient.DefaultRequestHeaders.TryGetValues("X-Panel-Auth", out var values))
        {
            var secret = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(secret))
            {
                return secret;
            }
        }

        throw new InvalidOperationException("Operations API shared secret is not configured on the panel HTTP client.");
    }
}

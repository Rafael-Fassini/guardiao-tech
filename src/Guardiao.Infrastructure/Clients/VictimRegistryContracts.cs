using System.Text.Json.Serialization;

namespace Guardiao.Infrastructure.Clients;

public sealed class VictimRegistryCaseResponse
{
    [JsonPropertyName("external_case_id")]
    public string ExternalCaseId { get; set; } = string.Empty;

    [JsonPropertyName("external_person_id")]
    public string ExternalPersonId { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("monitoring_status")]
    public string MonitoringStatus { get; set; } = string.Empty;

    [JsonPropertyName("consent_status")]
    public string ConsentStatus { get; set; } = string.Empty;

    [JsonPropertyName("is_bystander")]
    public bool IsBystander { get; set; }

    [JsonPropertyName("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class VictimRegistryCasesPageResponse
{
    [JsonPropertyName("items")]
    public List<VictimRegistryCaseResponse> Items { get; set; } = [];
}

public sealed class VictimRegistryMediaResponse
{
    [JsonPropertyName("media_id")]
    public string MediaId { get; set; } = string.Empty;

    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
}

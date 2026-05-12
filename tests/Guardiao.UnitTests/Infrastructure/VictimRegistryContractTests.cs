using System.Text.Json;
using Guardiao.Application.DTOs;
using Guardiao.Infrastructure.Clients;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class VictimRegistryContractTests
{
    [Fact]
    public void RestCasePayload_ShouldDeserializeExpectedFields()
    {
        const string json = """
        {
          "external_case_id": "case-123",
          "external_person_id": "person-456",
          "version": 9,
          "full_name": "Maria da Silva",
          "monitoring_status": "enabled",
          "consent_status": "granted",
          "is_bystander": false,
          "updated_at_utc": "2026-05-11T15:23:40Z"
        }
        """;

        var payload = JsonSerializer.Deserialize<VictimRegistryCaseResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal("case-123", payload.ExternalCaseId);
        Assert.Equal("person-456", payload.ExternalPersonId);
        Assert.Equal(9, payload.Version);
        Assert.Equal("enabled", payload.MonitoringStatus);
    }

    [Fact]
    public void RestPagePayload_ShouldDeserializeItemCollection()
    {
        const string json = """
        {
          "items": [
            {
              "external_case_id": "case-123",
              "external_person_id": "person-456",
              "version": 9,
              "full_name": "Maria da Silva",
              "monitoring_status": "enabled",
              "consent_status": "granted",
              "is_bystander": false,
              "updated_at_utc": "2026-05-11T15:23:40Z"
            }
          ]
        }
        """;

        var payload = JsonSerializer.Deserialize<VictimRegistryCasesPageResponse>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Single(payload.Items);
        Assert.Equal("case-123", payload.Items[0].ExternalCaseId);
    }

    [Fact]
    public void WebhookPayload_ShouldDeserializeExpectedFields()
    {
        const string json = """
        {
          "external_case_id": "case-123",
          "version": 9
        }
        """;

        var payload = JsonSerializer.Deserialize<VictimRegistryWebhookPayload>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(payload);
        Assert.Equal("case-123", payload.ExternalCaseId);
        Assert.Equal(9, payload.Version);
    }
}

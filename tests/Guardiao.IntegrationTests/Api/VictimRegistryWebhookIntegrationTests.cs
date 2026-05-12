using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Guardiao.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Sdk;

namespace Guardiao.IntegrationTests.Api;

public class VictimRegistryWebhookIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly HttpClient _client;
    private readonly GuardiaoApiFactory _factory;

    public VictimRegistryWebhookIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostWebhook_ShouldAcceptAndSyncProjection()
    {
        _factory.RegistryHandler.SetCase(
            "case-1",
            new
            {
                external_case_id = "case-1",
                external_person_id = "person-1",
                version = 3,
                full_name = "Maria da Silva",
                monitoring_status = "enabled",
                consent_status = "granted",
                is_bystander = false,
                updated_at_utc = DateTime.UtcNow
            });

        var response = await PostWebhookAsync("case-1", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await WaitForAsync(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            return await db.ProtectedCases.AnyAsync(x => x.ExternalCaseId.Value == "case-1" && x.Version == 3);
        });
    }

    [Fact]
    public async Task PostWebhook_ShouldRejectDuplicateDelivery()
    {
        _factory.RegistryHandler.SetCase(
            "case-duplicate",
            new
            {
                external_case_id = "case-duplicate",
                external_person_id = "person-duplicate",
                version = 1,
                full_name = "Case Duplicate",
                monitoring_status = "enabled",
                consent_status = "granted",
                is_bystander = false,
                updated_at_utc = DateTime.UtcNow
            });

        var deliveryId = Guid.NewGuid();

        var first = await PostWebhookAsync("case-duplicate", deliveryId);
        var second = await PostWebhookAsync("case-duplicate", deliveryId);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_ShouldRejectInvalidSignature()
    {
        var payload = JsonSerializer.Serialize(new { external_case_id = "case-invalid", version = 1 });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/victim-registry/webhooks")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Delivery-Id", Guid.NewGuid().ToString());
        request.Headers.Add("X-Event-Type", "case.changed");
        request.Headers.Add("X-Event-Timestamp", DateTimeOffset.UtcNow.ToString("O"));
        request.Headers.Add("X-Signature-SHA256", "bad-signature");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostWebhook_ShouldIgnoreStaleVersion()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var protectedCase = new Guardiao.Domain.Entities.ProtectedCase(
                new Guardiao.Domain.ValueObjects.ExternalCaseId("case-stale"),
                5,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guardiao.Domain.ValueObjects.MonitoringStatus.Enabled,
                Guardiao.Domain.ValueObjects.ConsentStatus.Granted);

            var person = new Guardiao.Domain.Entities.PersonProjection(
                new Guardiao.Domain.ValueObjects.ExternalPersonId("person-stale"),
                protectedCase.Id,
                "Existing Person",
                false,
                DateTime.UtcNow);

            protectedCase.BindPersonProjection(person.Id);

            db.ProtectedCases.Add(protectedCase);
            db.PersonProjections.Add(person);
            await db.SaveChangesAsync();
        }

        _factory.RegistryHandler.SetCase(
            "case-stale",
            new
            {
                external_case_id = "case-stale",
                external_person_id = "person-stale",
                version = 4,
                full_name = "Existing Person",
                monitoring_status = "enabled",
                consent_status = "granted",
                is_bystander = false,
                updated_at_utc = DateTime.UtcNow
            });

        var response = await PostWebhookAsync("case-stale", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        await WaitForAsync(async () =>
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            var saved = await db.ProtectedCases.SingleAsync(x => x.ExternalCaseId.Value == "case-stale");
            return saved.Version == 5;
        });
    }

    private async Task<HttpResponseMessage> PostWebhookAsync(string externalCaseId, Guid deliveryId)
    {
        var payload = JsonSerializer.Serialize(new { external_case_id = externalCaseId, version = 1 });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/integrations/victim-registry/webhooks")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("X-Delivery-Id", deliveryId.ToString());
        request.Headers.Add("X-Event-Type", "case.changed");
        request.Headers.Add("X-Event-Timestamp", DateTimeOffset.UtcNow.ToString("O"));
        request.Headers.Add("X-Signature-SHA256", ComputeSignature(payload, GuardiaoApiFactory.WebhookSecret));

        return await _client.SendAsync(request);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static async Task WaitForAsync(Func<Task<bool>> assertion, int attempts = 20)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await assertion())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new XunitException("Condition was not satisfied within the expected time.");
    }
}

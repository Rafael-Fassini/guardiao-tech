using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Guardiao.IntegrationTests.Api;

public class ApiReadinessIntegrationTests : IClassFixture<GuardiaoApiFactory>
{
    private readonly GuardiaoApiFactory _factory;

    public ApiReadinessIntegrationTests(GuardiaoApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetReady_ShouldReturnDependencyDetails()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReadyPayload>();
        Assert.NotNull(payload);
        Assert.Equal("Ready", payload!.Status);
        Assert.True(payload.DatabaseReachable);
        Assert.True(payload.MigrationsApplied);
        Assert.True(payload.ObjectStorageWritable);
    }

    private sealed record ReadyPayload(
        string Status,
        bool DatabaseReachable,
        bool MigrationsApplied,
        bool ObjectStorageWritable);
}

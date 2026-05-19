using System.Text.Json;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Xunit;

namespace Guardiao.UnitTests.Contracts;

[Trait("Category", "Contract")]
public class InternalEventSchemaContractTests
{
    [Fact]
    public void CandidateEventProjection_ShouldSerializeCriticalFields()
    {
        var candidateEvent = new BiometricCandidateEvent(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new CameraScope(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            new MatchScore(0.91),
            new DateTime(2026, 05, 19, 12, 00, 00, DateTimeKind.Utc));

        var json = JsonSerializer.Serialize(new
        {
            candidateEvent.Id,
            candidateEvent.ProtectedCaseId,
            SiteId = candidateEvent.CameraScope.SiteId,
            CameraId = candidateEvent.CameraScope.CameraId,
            MatchScore = candidateEvent.MatchScore.Value,
            candidateEvent.OccurredAtUtc
        });

        using var document = JsonDocument.Parse(json);
        Assert.True(document.RootElement.TryGetProperty("id", out _));
        Assert.True(document.RootElement.TryGetProperty("protectedCaseId", out _));
        Assert.True(document.RootElement.TryGetProperty("siteId", out _));
        Assert.True(document.RootElement.TryGetProperty("cameraId", out _));
        Assert.Equal(0.91, document.RootElement.GetProperty("matchScore").GetDouble(), 3);
    }
}

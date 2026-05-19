using Xunit;

namespace Guardiao.IntegrationTests.Worker;

[Trait("Category", "Replay")]
public class ApprovedCameraReplayIntegrationTests
{
    [Fact]
    public async Task ApprovedReplayFixture_ShouldPublishExpectedCandidateEvents()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "approved-camera-replay",
            "webcam-session.json");

        var harness = new ApprovedCameraReplayHarness();
        var result = await harness.RunAsync(fixturePath);

        Assert.Equal(result.ExpectedCandidateEvents, result.PublishedCandidateEvents);
        Assert.Contains(result.Counters.Keys, key => key.StartsWith("candidate_events_total", StringComparison.Ordinal));
        Assert.Contains(result.Gauges.Keys, key => key.StartsWith("match_latency_ms", StringComparison.Ordinal));
    }
}

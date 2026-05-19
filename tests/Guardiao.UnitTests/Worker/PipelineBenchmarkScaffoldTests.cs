using Xunit;

namespace Guardiao.UnitTests.Worker;

[Trait("Category", "Performance")]
public class PipelineBenchmarkScaffoldTests
{
    [Fact(Skip = "Benchmark scaffold only. Run manually when optimizing inference latency.")]
    public void CaptureToCandidateEvent_P95_BenchmarkScaffold()
    {
    }
}

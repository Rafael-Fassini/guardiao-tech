using Guardiao.Worker.Edge.Pipeline;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class FrameSamplerTests
{
    [Fact]
    public void ShouldProcess_ShouldHonorSamplingInterval()
    {
        var sampler = new FrameSampler(4);
        var start = new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc);

        Assert.True(sampler.ShouldProcess(start));
        Assert.False(sampler.ShouldProcess(start.AddMilliseconds(50)));
        Assert.True(sampler.ShouldProcess(start.AddMilliseconds(300)));
    }
}

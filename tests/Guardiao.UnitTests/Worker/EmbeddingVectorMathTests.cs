using Guardiao.Worker.Edge.Services;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class EmbeddingVectorMathTests
{
    [Fact]
    public void Normalize_ShouldProduceUnitVector()
    {
        var normalized = EmbeddingVectorMath.Normalize([3f, 4f]);

        Assert.Equal(0.6f, normalized[0], 3);
        Assert.Equal(0.8f, normalized[1], 3);
    }

    [Fact]
    public void CosineSimilarity_ShouldBeOne_ForIdenticalVectors()
    {
        var score = EmbeddingVectorMath.CosineSimilarity([0.5f, 0.5f], [0.5f, 0.5f]);

        Assert.Equal(1.0d, score, 6);
    }

    [Fact]
    public void CosineSimilarity_ShouldBeZero_WhenEitherVectorIsZero()
    {
        var score = EmbeddingVectorMath.CosineSimilarity([0f, 0f], [0.5f, 0.5f]);

        Assert.Equal(0.0d, score, 6);
    }
}

using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Worker;

public class RestrictedGalleryMatcherTests
{
    [Fact]
    public void MatchWithinScope_ShouldReturnBestMatch_WhenEmbeddingIsClose()
    {
        var protectedCaseId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var provider = CreateProvider(protectedCaseId, siteId, false, Enumerable.Repeat(0.5f, 16).ToArray());
        var matcher = new RestrictedGalleryMatcherPort(provider, new EdgeMetricsCollector(), Options.Create(new EdgeWorkerOptions
        {
            MatchThreshold = 0.80
        }));

        var result = matcher.MatchWithinScope(Enumerable.Repeat(0.5f, 16).ToArray(), protectedCaseId, siteId);

        Assert.True(result.IsMatch);
        Assert.True(result.Score >= 0.99);
    }

    [Fact]
    public void MatchWithinScope_ShouldRejectBelowThreshold()
    {
        var protectedCaseId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var provider = CreateProvider(protectedCaseId, siteId, false, Enumerable.Repeat(0.1f, 16).ToArray());
        var matcher = new RestrictedGalleryMatcherPort(provider, new EdgeMetricsCollector(), Options.Create(new EdgeWorkerOptions
        {
            MatchThreshold = 0.95
        }));

        var result = matcher.MatchWithinScope(Enumerable.Repeat(0.9f, 16).ToArray(), protectedCaseId, siteId);

        Assert.False(result.IsMatch);
    }

    [Fact]
    public void MatchWithinScope_ShouldRejectBystanderEvenWhenScoreIsHigh()
    {
        var protectedCaseId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var provider = CreateProvider(protectedCaseId, siteId, true, Enumerable.Repeat(0.5f, 16).ToArray());
        var matcher = new RestrictedGalleryMatcherPort(provider, new EdgeMetricsCollector(), Options.Create(new EdgeWorkerOptions
        {
            MatchThreshold = 0.10
        }));

        var result = matcher.MatchWithinScope(Enumerable.Repeat(0.5f, 16).ToArray(), protectedCaseId, siteId);

        Assert.False(result.IsMatch);
        Assert.True(result.IsBystander);
    }

    private static RestrictedGalleryProvider CreateProvider(Guid protectedCaseId, Guid siteId, bool isBystander, float[] embedding)
    {
        return new RestrictedGalleryProvider(Options.Create(new EdgeWorkerOptions
        {
            RestrictedGallery =
            [
                new RestrictedGallerySeedOptions
                {
                    ProtectedCaseId = protectedCaseId,
                    SiteId = siteId,
                    PersonProjectionId = Guid.NewGuid(),
                    ExternalPersonId = "person-1",
                    IsBystander = isBystander,
                    Embedding = embedding
                }
            ]
        }));
    }
}

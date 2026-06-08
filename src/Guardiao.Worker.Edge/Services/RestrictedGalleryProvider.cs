using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Pipeline;
using Microsoft.Extensions.Options;

namespace Guardiao.Worker.Edge.Services;

public interface IRestrictedGalleryProvider
{
    IReadOnlyCollection<GalleryCandidate> GetByScope(Guid protectedCaseId, Guid siteId);
    IReadOnlyCollection<GalleryCandidate> GetByProtectedCase(Guid protectedCaseId);
}

public sealed class RestrictedGalleryProvider : IRestrictedGalleryProvider
{
    private readonly List<GalleryCandidate> _entries;

    public RestrictedGalleryProvider(IOptions<EdgeWorkerOptions> options)
    {
        _entries = options.Value.RestrictedGallery
            .Select(x => new GalleryCandidate(
                x.ProtectedCaseId,
                x.SiteId,
                x.PersonProjectionId,
                x.ExternalPersonId,
                x.IsBystander,
                EmbeddingVectorMath.Normalize(x.Embedding)))
            .ToList();
    }

    public IReadOnlyCollection<GalleryCandidate> GetByScope(Guid protectedCaseId, Guid siteId)
    {
        return _entries
            .Where(x => x.ProtectedCaseId == protectedCaseId && x.SiteId == siteId)
            .ToArray();
    }

    public IReadOnlyCollection<GalleryCandidate> GetByProtectedCase(Guid protectedCaseId)
    {
        return _entries
            .Where(x => x.ProtectedCaseId == protectedCaseId)
            .ToArray();
    }
}

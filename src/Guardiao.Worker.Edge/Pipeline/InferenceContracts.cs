namespace Guardiao.Worker.Edge.Pipeline;

public sealed record DetectedFaceCandidate(Guid DetectionId, byte[] CropBytes, double DetectionScore);

public sealed record GalleryCandidate(
    Guid ProtectedCaseId,
    Guid SiteId,
    Guid PersonProjectionId,
    string ExternalPersonId,
    bool IsBystander,
    IReadOnlyCollection<float> Embedding);

public sealed record GalleryMatchResult(
    Guid ProtectedCaseId,
    Guid SiteId,
    Guid PersonProjectionId,
    string ExternalPersonId,
    double Score,
    bool IsMatch,
    bool IsBystander);

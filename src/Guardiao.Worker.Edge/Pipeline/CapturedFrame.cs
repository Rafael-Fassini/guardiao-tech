namespace Guardiao.Worker.Edge.Pipeline;

public sealed record CapturedFrame(
    Guid CameraId,
    Guid SiteId,
    Guid ProtectedCaseId,
    byte[] Bytes,
    DateTime CapturedAtUtc,
    long Sequence);

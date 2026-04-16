using Guardiao.Domain.Entities;
using Guardiao.Domain.Ports;

namespace Guardiao.Infrastructure.Adapters;

public sealed class NormalizedDetectionEventPayload
{
    public Guid CameraSourceId { get; init; }
    public Guid? DetectedProtectedPersonId { get; init; }
    public Guid? DetectedRestrictedPersonId { get; init; }
    public DateTime Timestamp { get; init; }
}

public class NormalizedDetectionEventAdapter : IDetectionEventAdapter
{
    public DetectionEvent Adapt(object externalPayload)
    {
        if (externalPayload is not NormalizedDetectionEventPayload payload)
        {
            throw new ArgumentException("Unsupported payload type for detection event adapter.");
        }

        return new DetectionEvent(
            payload.CameraSourceId,
            payload.DetectedProtectedPersonId,
            payload.DetectedRestrictedPersonId,
            payload.Timestamp);
    }
}

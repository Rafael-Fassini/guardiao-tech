using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Factories;

public class DetectionEventFactory
{
    public DetectionEvent Create(Guid cameraSourceId, Guid? detectedProtectedPersonId, Guid? detectedRestrictedPersonId, DateTime timestamp)
    {
        // Add validation or default logic if needed
        if (cameraSourceId == Guid.Empty)
            throw new ArgumentException("CameraSourceId is required");
        return new DetectionEvent(cameraSourceId, detectedProtectedPersonId, detectedRestrictedPersonId, timestamp);
    }
}

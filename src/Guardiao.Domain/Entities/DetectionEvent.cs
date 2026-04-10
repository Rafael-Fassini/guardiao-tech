namespace Guardiao.Domain.Entities;

public class DetectionEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CameraSourceId { get; private set; }
    public Guid? DetectedProtectedPersonId { get; private set; }
    public Guid? DetectedRestrictedPersonId { get; private set; }
    public DateTime Timestamp { get; private set; }

    public DetectionEvent(Guid cameraSourceId, Guid? detectedProtectedPersonId, Guid? detectedRestrictedPersonId, DateTime timestamp)
    {
        CameraSourceId = cameraSourceId;
        DetectedProtectedPersonId = detectedProtectedPersonId;
        DetectedRestrictedPersonId = detectedRestrictedPersonId;
        Timestamp = timestamp;
    }
}

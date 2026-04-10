namespace Guardiao.Application.DTOs;

public class DetectionEventDto
{
    public Guid CameraSourceId { get; set; }
    public Guid? DetectedProtectedPersonId { get; set; }
    public Guid? DetectedRestrictedPersonId { get; set; }
    public DateTime Timestamp { get; set; }
}

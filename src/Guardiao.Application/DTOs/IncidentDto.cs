namespace Guardiao.Application.DTOs;

public class IncidentDto
{
    public Guid Id { get; set; }
    public Guid ProtectiveCaseId { get; set; }
    public Guid DetectionEventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; }
}

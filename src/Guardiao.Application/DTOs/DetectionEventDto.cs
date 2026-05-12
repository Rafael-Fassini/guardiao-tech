namespace Guardiao.Application.DTOs;

public class BiometricCandidateEventDto
{
    public Guid ProtectedCaseId { get; set; }
    public Guid SiteId { get; set; }
    public Guid CameraId { get; set; }
    public double MatchScore { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

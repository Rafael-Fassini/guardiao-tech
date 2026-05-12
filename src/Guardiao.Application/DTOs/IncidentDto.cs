using Guardiao.Domain.Enums;

namespace Guardiao.Application.DTOs;

public class IncidentDto
{
    public Guid Id { get; set; }
    public Guid ProtectedCaseId { get; set; }
    public Guid CandidateEventId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public IncidentStatus Status { get; set; }
}

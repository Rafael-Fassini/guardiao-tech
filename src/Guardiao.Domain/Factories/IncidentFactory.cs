using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Factories;

public class IncidentFactory
{
    public Incident Create(Guid protectiveCaseId, Guid detectionEventId, string status)
    {
        if (protectiveCaseId == Guid.Empty || detectionEventId == Guid.Empty)
            throw new ArgumentException("Case and Event are required");
        return new Incident(protectiveCaseId, detectionEventId, status);
    }
}

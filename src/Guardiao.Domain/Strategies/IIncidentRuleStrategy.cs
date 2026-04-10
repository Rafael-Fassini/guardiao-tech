using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Strategies;

public interface IIncidentRuleStrategy
{
    bool ShouldCreateIncident(DetectionEvent detectionEvent, ProtectiveCase protectiveCase);
}

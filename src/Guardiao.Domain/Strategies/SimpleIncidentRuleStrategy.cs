using Guardiao.Domain.Entities;

namespace Guardiao.Domain.Strategies;

public class SimpleIncidentRuleStrategy : IIncidentRuleStrategy
{
    public bool ShouldCreateIncident(DetectionEvent detectionEvent, ProtectiveCase protectiveCase)
    {
        // Regra: se o evento referencia vítima e agressor do mesmo caso, cria incidente
        if (detectionEvent.DetectedProtectedPersonId == protectiveCase.ProtectedPersonId &&
            detectionEvent.DetectedRestrictedPersonId == protectiveCase.RestrictedPersonId)
        {
            return true;
        }
        return false;
    }
}

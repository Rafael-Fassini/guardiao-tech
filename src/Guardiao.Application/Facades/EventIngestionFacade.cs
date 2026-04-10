using Guardiao.Application.DTOs;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Factories;
using Guardiao.Domain.Strategies;

namespace Guardiao.Application.Facades;

public class EventIngestionFacade
{
    private readonly DetectionEventFactory _detectionEventFactory;
    private readonly IncidentFactory _incidentFactory;
    private readonly IIncidentRuleStrategy _incidentRuleStrategy;
    // Repositórios e serviços omitidos para MVP

    public EventIngestionFacade(
        DetectionEventFactory detectionEventFactory,
        IncidentFactory incidentFactory,
        IIncidentRuleStrategy incidentRuleStrategy)
    {
        _detectionEventFactory = detectionEventFactory;
        _incidentFactory = incidentFactory;
        _incidentRuleStrategy = incidentRuleStrategy;
    }

    public (DetectionEvent, Incident?) IngestDetectionEvent(DetectionEventDto dto, ProtectiveCase protectiveCase)
    {
        var detectionEvent = _detectionEventFactory.Create(
            dto.CameraSourceId,
            dto.DetectedProtectedPersonId,
            dto.DetectedRestrictedPersonId,
            dto.Timestamp);

        Incident? incident = null;
        if (_incidentRuleStrategy.ShouldCreateIncident(detectionEvent, protectiveCase))
        {
            incident = _incidentFactory.Create(protectiveCase.Id, detectionEvent.Id, "Open");
        }
        return (detectionEvent, incident);
    }
}

using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly GuardiaoDbContext _context;

    public IncidentRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public Task<Incident?> GetByIdAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        return _context.Incidents.FirstOrDefaultAsync(x => x.Id == incidentId, cancellationToken);
    }

    public Task<Incident?> GetByCandidateEventIdAsync(Guid candidateEventId, CancellationToken cancellationToken = default)
    {
        return _context.Incidents.FirstOrDefaultAsync(x => x.CandidateEventId == candidateEventId, cancellationToken);
    }

    public Task<Incident?> FindLatestActiveByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        return _context.Incidents
            .Where(x => x.ProtectedCaseId == protectedCaseId &&
                        x.Status != Guardiao.Domain.Enums.IncidentStatus.Dismissed &&
                        x.Status != Guardiao.Domain.Enums.IncidentStatus.Escalated)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Incident?> FindLatestActiveByCaseAndCameraScopeAsync(
        Guid protectedCaseId,
        CameraScope cameraScope,
        DateTime createdAfterUtc,
        CancellationToken cancellationToken = default)
    {
        return (
            from incident in _context.Incidents
            join candidateEvent in _context.BiometricCandidateEvents on incident.CandidateEventId equals candidateEvent.Id
            where incident.ProtectedCaseId == protectedCaseId &&
                  incident.Status != Guardiao.Domain.Enums.IncidentStatus.Dismissed &&
                  incident.Status != Guardiao.Domain.Enums.IncidentStatus.Escalated &&
                  incident.CreatedAtUtc >= createdAfterUtc &&
                  candidateEvent.CameraScope.SiteId == cameraScope.SiteId &&
                  candidateEvent.CameraScope.CameraId == cameraScope.CameraId
            orderby incident.CreatedAtUtc descending
            select incident)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        _context.Incidents.Update(incident);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
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

    public Task<Incident?> FindLatestActiveByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        return _context.Incidents
            .Where(x => x.ProtectedCaseId == protectedCaseId &&
                        x.Status != Guardiao.Domain.Enums.IncidentStatus.Dismissed &&
                        x.Status != Guardiao.Domain.Enums.IncidentStatus.Escalated)
            .OrderByDescending(x => x.CreatedAtUtc)
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

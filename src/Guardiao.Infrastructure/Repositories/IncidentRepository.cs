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

    public async Task AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

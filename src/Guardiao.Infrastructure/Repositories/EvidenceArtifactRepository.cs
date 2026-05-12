using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public interface IEvidenceArtifactRepository
{
    Task AddAsync(EvidenceArtifact artifact, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<EvidenceArtifact>> ListByIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default);
}

public sealed class EvidenceArtifactRepository : IEvidenceArtifactRepository
{
    private readonly GuardiaoDbContext _context;

    public EvidenceArtifactRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EvidenceArtifact artifact, CancellationToken cancellationToken = default)
    {
        _context.EvidenceArtifacts.Add(artifact);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<EvidenceArtifact>> ListByIncidentAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        return await _context.EvidenceArtifacts
            .Where(x => x.IncidentId == incidentId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}

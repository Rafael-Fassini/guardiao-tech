using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class CorrelationDecisionRepository : ICorrelationDecisionRepository
{
    private readonly GuardiaoDbContext _context;

    public CorrelationDecisionRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CorrelationDecision decision, CancellationToken cancellationToken = default)
    {
        _context.CorrelationDecisions.Add(decision);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CorrelationDecision>> ListByCandidateEventAsync(Guid candidateEventId, CancellationToken cancellationToken = default)
    {
        return await _context.CorrelationDecisions
            .Where(x => x.CandidateEventId == candidateEventId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }
}

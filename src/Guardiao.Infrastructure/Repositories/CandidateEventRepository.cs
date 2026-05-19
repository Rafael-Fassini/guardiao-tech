using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;

namespace Guardiao.Infrastructure.Repositories;

public sealed class CandidateEventRepository : ICandidateEventRepository
{
    private readonly GuardiaoDbContext _context;

    public CandidateEventRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BiometricCandidateEvent candidateEvent, CancellationToken cancellationToken = default)
    {
        _context.BiometricCandidateEvents.Add(candidateEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

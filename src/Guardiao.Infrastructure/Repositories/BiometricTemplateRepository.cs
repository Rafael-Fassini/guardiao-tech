using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class BiometricTemplateRepository : IBiometricTemplateRepository
{
    private readonly GuardiaoDbContext _context;

    public BiometricTemplateRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BiometricTemplate biometricTemplate, CancellationToken cancellationToken = default)
    {
        _context.BiometricTemplates.Add(biometricTemplate);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BiometricTemplate>> ListByPersonAsync(Guid personProjectionId, CancellationToken cancellationToken = default)
    {
        return await _context.BiometricTemplates
            .Where(x => x.PersonProjectionId == personProjectionId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}

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

    public Task<BiometricTemplate?> GetByIdAsync(Guid biometricTemplateId, CancellationToken cancellationToken = default)
    {
        return _context.BiometricTemplates.FirstOrDefaultAsync(x => x.Id == biometricTemplateId, cancellationToken);
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

    public async Task<IReadOnlyCollection<BiometricTemplate>> ListByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        return await _context.BiometricTemplates
            .Join(
                _context.PersonProjections,
                template => template.PersonProjectionId,
                projection => projection.Id,
                (template, projection) => new { template, projection })
            .Where(x => x.projection.ProtectedCaseId == protectedCaseId)
            .OrderByDescending(x => x.template.CreatedAt)
            .Select(x => x.template)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<BiometricTemplate>> ListActiveByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        return await _context.BiometricTemplates
            .Join(
                _context.PersonProjections,
                template => template.PersonProjectionId,
                projection => projection.Id,
                (template, projection) => new { template, projection })
            .Where(x => x.projection.ProtectedCaseId == protectedCaseId && x.template.IsActive)
            .OrderByDescending(x => x.template.CreatedAt)
            .Select(x => x.template)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(BiometricTemplate biometricTemplate, CancellationToken cancellationToken = default)
    {
        _context.BiometricTemplates.Update(biometricTemplate);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

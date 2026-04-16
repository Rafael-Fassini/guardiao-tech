using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;

namespace Guardiao.Infrastructure.Repositories;

public class InstitutionRepository : IInstitutionRepositoryPort
{
    private readonly GuardiaoDbContext _context;

    public InstitutionRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task<Institution> AddAsync(Institution institution)
    {
        // Outbound adapter: persists domain entity using EF Core.
        _context.Institutions.Add(institution);
        await _context.SaveChangesAsync();
        return institution;
    }
}

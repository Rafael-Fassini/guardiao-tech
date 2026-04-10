using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public class InstitutionRepository : IInstitutionRepository
{
    private readonly GuardiaoDbContext _context;
    public InstitutionRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task<Institution> AddAsync(Institution institution)
    {
        _context.Institutions.Add(institution);
        await _context.SaveChangesAsync();
        return institution;
    }
}

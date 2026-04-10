using Guardiao.Domain.Entities;

namespace Guardiao.Infrastructure.Repositories;

public interface IInstitutionRepository
{
    Task<Institution> AddAsync(Institution institution);
    // Outros métodos podem ser adicionados conforme necessário
}

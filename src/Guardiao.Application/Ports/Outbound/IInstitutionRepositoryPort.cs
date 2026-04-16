using Guardiao.Domain.Entities;

namespace Guardiao.Application.Ports.Outbound;

public interface IInstitutionRepositoryPort
{
    Task<Institution> AddAsync(Institution institution);
}

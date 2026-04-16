using Guardiao.Application.DTOs;

namespace Guardiao.Application.Ports.Inbound;

public interface ICreateInstitutionUseCase
{
    Task<InstitutionDto> ExecuteAsync(CreateInstitutionCommand command);
}

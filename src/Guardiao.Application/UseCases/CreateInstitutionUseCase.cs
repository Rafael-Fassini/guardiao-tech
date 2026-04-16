using Guardiao.Application.DTOs;
using Guardiao.Application.Ports.Inbound;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;

namespace Guardiao.Application.UseCases;

public class CreateInstitutionUseCase : ICreateInstitutionUseCase
{
    private readonly IInstitutionRepositoryPort _institutionRepositoryPort;

    public CreateInstitutionUseCase(IInstitutionRepositoryPort institutionRepositoryPort)
    {
        _institutionRepositoryPort = institutionRepositoryPort;
    }

    public async Task<InstitutionDto> ExecuteAsync(CreateInstitutionCommand command)
    {
        ValidationService.ValidateString(command.Name, nameof(command.Name), 200);
        ValidationService.ValidateString(command.Address, nameof(command.Address), 300);

        var institution = new Institution(command.Name, command.Address);
        var created = await _institutionRepositoryPort.AddAsync(institution);

        return new InstitutionDto(created.Id, created.Name, created.Address, created.CreatedAt);
    }
}

using Guardiao.Api.Contracts;
using Guardiao.Application.DTOs;
using Guardiao.Application.Ports.Inbound;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstitutionsController : ControllerBase
{
    private readonly ICreateInstitutionUseCase _createInstitutionUseCase;

    public InstitutionsController(ICreateInstitutionUseCase createInstitutionUseCase)
    {
        _createInstitutionUseCase = createInstitutionUseCase;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstitutionRequest request)
    {
        // HTTP adapter only maps transport request to application command.
        var command = new CreateInstitutionCommand(request.Name, request.Address);
        var result = await _createInstitutionUseCase.ExecuteAsync(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(Guid id)
    {
        // Implementação simplificada para MVP
        return Ok();
    }
}

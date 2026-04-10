using Guardiao.Api.Contracts;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InstitutionsController : ControllerBase
{
    private readonly IInstitutionRepository _institutionRepository;

    public InstitutionsController(IInstitutionRepository institutionRepository)
    {
        _institutionRepository = institutionRepository;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInstitutionRequest request)
    {
        ValidationService.ValidateString(request.Name, nameof(request.Name));
        ValidationService.ValidateString(request.Address, nameof(request.Address));
        var institution = new Institution(request.Name, request.Address);
        var result = await _institutionRepository.AddAsync(institution);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Implementação simplificada para MVP
        return Ok();
    }
}

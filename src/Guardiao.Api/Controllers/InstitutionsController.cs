using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Application.DTOs;
using Guardiao.Application.Ports.Inbound;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class InstitutionsController : ControllerBase
{
    private readonly ICreateInstitutionUseCase _createInstitutionUseCase;
    private readonly GuardiaoDbContext _dbContext;

    public InstitutionsController(ICreateInstitutionUseCase createInstitutionUseCase, GuardiaoDbContext dbContext)
    {
        _createInstitutionUseCase = createInstitutionUseCase;
        _dbContext = dbContext;
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> Create([FromBody] CreateInstitutionRequest request)
    {
        // HTTP adapter only maps transport request to application command.
        var command = new CreateInstitutionCommand(request.Name, request.Address);
        var result = await _createInstitutionUseCase.ExecuteAsync(command);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Institutions
            .OrderBy(x => x.Name)
            .Select(x => new InstitutionDto(x.Id, x.Name, x.Address, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _dbContext.Institutions
            .Where(x => x.Id == id)
            .Select(x => new InstitutionDto(x.Id, x.Name, x.Address, x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? NotFound() : Ok(item);
    }
}

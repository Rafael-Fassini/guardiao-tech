using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/sites")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class SitesController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public SitesController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Set<Guardiao.Domain.Entities.Site>()
            .OrderBy(x => x.Name)
            .Select(x => new SiteResponse(
                x.Id,
                x.InstitutionId,
                x.Name,
                x.AddressLine))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}

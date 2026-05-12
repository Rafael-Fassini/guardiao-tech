using Guardiao.Api.Infrastructure;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/cameras")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class CamerasController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;

    public CamerasController(GuardiaoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Set<Guardiao.Domain.Entities.Camera>()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.SiteId,
                x.Name,
                x.StreamEndpoint,
                x.IsEnabled
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}

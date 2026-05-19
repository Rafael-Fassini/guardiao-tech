using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class MonitoringRuleRepository : IMonitoringRuleRepository
{
    private readonly GuardiaoDbContext _context;

    public MonitoringRuleRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<MonitoringRule>> ListByCaseAsync(Guid protectedCaseId, CancellationToken cancellationToken = default)
    {
        return await _context.MonitoringRules
            .Where(x => x.ProtectedCaseId == protectedCaseId)
            .ToListAsync(cancellationToken);
    }
}

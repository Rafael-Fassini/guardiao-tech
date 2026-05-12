using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class SyncCursorRepository : ISyncCursorRepository
{
    private readonly GuardiaoDbContext _context;

    public SyncCursorRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public async Task<DateTime?> GetLastCursorAsync(string cursorName, CancellationToken cancellationToken = default)
    {
        var record = await _context.SyncCursors.FirstOrDefaultAsync(x => x.Name == cursorName, cancellationToken);
        return record?.CursorUtc;
    }

    public async Task SaveLastCursorAsync(string cursorName, DateTime cursorUtc, CancellationToken cancellationToken = default)
    {
        var record = await _context.SyncCursors.FirstOrDefaultAsync(x => x.Name == cursorName, cancellationToken);
        if (record is null)
        {
            _context.SyncCursors.Add(new SyncCursorRecord { Name = cursorName, CursorUtc = cursorUtc });
        }
        else
        {
            record.CursorUtc = cursorUtc;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

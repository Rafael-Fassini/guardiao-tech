using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Infrastructure.Repositories;

public sealed class CaseProjectionRepository : ICaseProjectionRepository
{
    private readonly GuardiaoDbContext _context;

    public CaseProjectionRepository(GuardiaoDbContext context)
    {
        _context = context;
    }

    public Task<ProtectedCase?> GetByExternalIdAsync(ExternalCaseId externalCaseId, CancellationToken cancellationToken = default)
    {
        return _context.ProtectedCases.FirstOrDefaultAsync(x => x.ExternalCaseId.Value == externalCaseId.Value, cancellationToken);
    }

    public async Task UpsertAsync(ProtectedCase protectedCase, PersonProjection personProjection, CancellationToken cancellationToken = default)
    {
        var existingCase = await _context.ProtectedCases.FirstOrDefaultAsync(
            x => x.ExternalCaseId.Value == protectedCase.ExternalCaseId.Value,
            cancellationToken);

        if (existingCase is null)
        {
            _context.ProtectedCases.Add(protectedCase);
            _context.PersonProjections.Add(personProjection);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        existingCase.SynchronizeFromSource(
            protectedCase.Version,
            protectedCase.MonitoringStatus,
            protectedCase.ConsentStatus,
            protectedCase.LastSynchronizedAt);

        var existingProjection = await _context.PersonProjections.FirstOrDefaultAsync(
            x => x.ProtectedCaseId == existingCase.Id,
            cancellationToken);

        if (existingProjection is null)
        {
            existingCase.BindPersonProjection(personProjection.Id);
            _context.PersonProjections.Add(personProjection);
        }
        else
        {
            existingProjection.RefreshFromSource(personProjection.FullName, personProjection.IsBystander, personProjection.SourceUpdatedAtUtc);
            existingCase.BindPersonProjection(existingProjection.Id);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSyncFailureAsync(ExternalCaseId externalCaseId, string failureReason, DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var existingCase = await _context.ProtectedCases.FirstOrDefaultAsync(
            x => x.ExternalCaseId.Value == externalCaseId.Value,
            cancellationToken);

        if (existingCase is null)
        {
            return;
        }

        existingCase.MarkSyncFailure(failureReason, occurredAtUtc);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

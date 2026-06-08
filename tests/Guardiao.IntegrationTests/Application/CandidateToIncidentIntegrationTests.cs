using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Caching;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Guardiao.Infrastructure.Repositories;
using Guardiao.Infrastructure.System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.IntegrationTests.Application;

public class CandidateToIncidentIntegrationTests
{
    [Fact]
    public async Task ConsumeAsync_ShouldCreateIncidentAndDecisionTrail_EndToEnd()
    {
        await using var db = CreateDbContext();
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-e2e"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        db.ProtectedCases.Add(protectedCase);
        db.MonitoringRules.Add(new MonitoringRule(
            protectedCase.Id,
            new CameraScope(Guid.NewGuid(), Guid.NewGuid()),
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true));
        await db.SaveChangesAsync();

        var rule = await db.MonitoringRules.SingleAsync();
        var candidate = new BiometricCandidateEvent(
            protectedCase.Id,
            rule.CameraScope,
            new MatchScore(0.91),
            DateTime.UtcNow);

        var service = CreateService(db);
        var result = await service.ConsumeAsync(candidate);

        Assert.NotNull(result.Incident);
        Assert.True(result.Decision.CreatesIncident);
        Assert.Equal(1, await db.Incidents.CountAsync());
        Assert.Equal(1, await db.CorrelationDecisions.CountAsync());
    }

    [Fact]
    public async Task ConsumeAsync_ShouldSuppressWithinCooldown()
    {
        await using var db = CreateDbContext();
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-cooldown"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        db.ProtectedCases.Add(protectedCase);
        db.MonitoringRules.Add(new MonitoringRule(
            protectedCase.Id,
            scope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true));
        db.Incidents.Add(new Incident(protectedCase.Id, Guid.NewGuid()));
        await db.SaveChangesAsync();

        var candidate = new BiometricCandidateEvent(protectedCase.Id, scope, new MatchScore(0.95), DateTime.UtcNow);

        var service = CreateService(db);
        var result = await service.ConsumeAsync(candidate);

        Assert.False(result.Decision.CreatesIncident);
        Assert.Equal("COOLDOWN_ACTIVE", result.Decision.ReasonCode.Value);
        Assert.Equal(1, await db.Incidents.CountAsync());
    }

    private static CandidateEventCorrelationService CreateService(GuardiaoDbContext db)
    {
        return new CandidateEventCorrelationService(
            new CaseProjectionRepository(db),
            new MonitoringRuleRepository(db),
            new CandidateEventRepository(db),
            new CorrelationDecisionRepository(db),
            new IncidentRepository(db),
            new AuditLogRepository(db),
            new RedisShortLivedStateStore(Options.Create(new RedisOptions { DefaultTtlSeconds = 30 })),
            new SystemClock(),
            new CorrelationEngineOptions
            {
                CooldownWindow = TimeSpan.FromMinutes(5),
                DuplicateSuppressionWindow = TimeSpan.FromSeconds(30)
            });
    }

    private static GuardiaoDbContext CreateDbContext()
    {
        return new GuardiaoDbContext(
            new DbContextOptionsBuilder<GuardiaoDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
    }
}

using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
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
    public async Task ConsumeAsync_ShouldCreateIncidentOnlyAfterCoPresenceMatch_EndToEnd()
    {
        await using var db = CreateDbContext();
        var institutionId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var cameraId = Guid.NewGuid();

        var protectedWoman = new ProtectedCase(
            new ExternalCaseId("case-victim-e2e"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);
        var aggressor = new ProtectedCase(
            new ExternalCaseId("case-aggressor-e2e"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.Aggressor);

        db.ProtectedCases.AddRange(protectedWoman, aggressor);
        db.MonitoringRules.AddRange(
            new MonitoringRule(
                protectedWoman.Id,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true),
            new MonitoringRule(
                aggressor.Id,
                new CameraScope(siteId, cameraId),
                new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
                true));
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var firstEvent = new BiometricCandidateEvent(
            protectedWoman.Id,
            new CameraScope(siteId, cameraId),
            new MatchScore(0.91),
            DateTime.UtcNow.AddSeconds(-45));
        var firstResult = await service.ConsumeAsync(firstEvent);

        Assert.False(firstResult.Decision.CreatesIncident);
        Assert.Equal("CO_PRESENCE_NOT_FOUND", firstResult.Decision.ReasonCode.Value);
        Assert.Equal(0, await db.Incidents.CountAsync());

        var secondEvent = new BiometricCandidateEvent(
            aggressor.Id,
            new CameraScope(siteId, cameraId),
            new MatchScore(0.94),
            DateTime.UtcNow);
        var secondResult = await service.ConsumeAsync(secondEvent);

        Assert.NotNull(secondResult.Incident);
        Assert.True(secondResult.Decision.CreatesIncident);
        Assert.Equal("CO_PRESENCE_MATCH", secondResult.Decision.ReasonCode.Value);
        Assert.Equal(protectedWoman.Id, secondResult.Incident!.ProtectedCaseId);
        Assert.Equal(1, await db.Incidents.CountAsync());
        Assert.Equal(2, await db.CorrelationDecisions.CountAsync());
    }

    [Fact]
    public async Task ConsumeAsync_ShouldNotCreateIncident_WhenCounterpartIsOutsideWindow()
    {
        await using var db = CreateDbContext();
        var institutionId = Guid.NewGuid();
        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());

        var protectedWoman = new ProtectedCase(
            new ExternalCaseId("case-victim-window"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);
        var aggressor = new ProtectedCase(
            new ExternalCaseId("case-aggressor-window"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.Aggressor);

        db.ProtectedCases.AddRange(protectedWoman, aggressor);
        db.MonitoringRules.AddRange(
            new MonitoringRule(protectedWoman.Id, scope, new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)), true),
            new MonitoringRule(aggressor.Id, scope, new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)), true));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ConsumeAsync(new BiometricCandidateEvent(
            protectedWoman.Id,
            scope,
            new MatchScore(0.90),
            DateTime.UtcNow.AddMinutes(-10)));

        var result = await service.ConsumeAsync(new BiometricCandidateEvent(
            aggressor.Id,
            scope,
            new MatchScore(0.95),
            DateTime.UtcNow));

        Assert.False(result.Decision.CreatesIncident);
        Assert.Equal("CO_PRESENCE_NOT_FOUND", result.Decision.ReasonCode.Value);
        Assert.Equal(0, await db.Incidents.CountAsync());
    }

    [Fact]
    public async Task ConsumeAsync_ShouldSuppressDuplicateIncident_WhenEncounterIsAlreadyOpen()
    {
        await using var db = CreateDbContext();
        var institutionId = Guid.NewGuid();
        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());

        var protectedWoman = new ProtectedCase(
            new ExternalCaseId("case-victim-cooldown"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);
        var aggressor = new ProtectedCase(
            new ExternalCaseId("case-aggressor-cooldown"),
            1,
            institutionId,
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.Aggressor);

        db.ProtectedCases.AddRange(protectedWoman, aggressor);
        db.MonitoringRules.AddRange(
            new MonitoringRule(protectedWoman.Id, scope, new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)), true),
            new MonitoringRule(aggressor.Id, scope, new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)), true));
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ConsumeAsync(new BiometricCandidateEvent(
            protectedWoman.Id,
            scope,
            new MatchScore(0.90),
            DateTime.UtcNow.AddSeconds(-30)));

        var firstAggressorResult = await service.ConsumeAsync(new BiometricCandidateEvent(
            aggressor.Id,
            scope,
            new MatchScore(0.95),
            DateTime.UtcNow.AddSeconds(-10)));

        Assert.True(firstAggressorResult.Decision.CreatesIncident);

        var duplicateAggressorResult = await service.ConsumeAsync(new BiometricCandidateEvent(
            aggressor.Id,
            scope,
            new MatchScore(0.96),
            DateTime.UtcNow));

        Assert.False(duplicateAggressorResult.Decision.CreatesIncident);
        Assert.Equal("ENCOUNTER_ALREADY_OPEN", duplicateAggressorResult.Decision.ReasonCode.Value);
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
                CoPresenceWindow = TimeSpan.FromMinutes(5),
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

using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Guardiao.UnitTests.Application;

public class CandidateEventCorrelationServiceTests
{
    [Fact]
    public async Task ConsumeAsync_ShouldNotCreateIncident_WhenNoCounterpartIsDetected()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-1"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);

        var candidateEvent = new BiometricCandidateEvent(
            protectedCase.Id,
            new CameraScope(Guid.NewGuid(), Guid.NewGuid()),
            new MatchScore(0.92),
            DateTime.UtcNow);

        var rule = new MonitoringRule(
            protectedCase.Id,
            candidateEvent.CameraScope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true);

        var cases = new Mock<ICaseProjectionRepository>();
        cases.Setup(x => x.GetByIdAsync(protectedCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(protectedCase);

        var rules = new Mock<IMonitoringRuleRepository>();
        rules.Setup(x => x.ListByCaseAsync(protectedCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([rule]);

        var candidateEvents = new Mock<ICandidateEventRepository>();
        candidateEvents.Setup(x => x.GetByIdAsync(candidateEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync((BiometricCandidateEvent?)null);
        candidateEvents.Setup(x => x.ListRecentByCameraScopeAsync(
                candidateEvent.CameraScope,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BiometricCandidateEvent>());

        var incidents = new Mock<IIncidentRepository>();

        var state = new Mock<IShortLivedStatePort>();
        state.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var service = CreateService(
            cases.Object,
            rules.Object,
            candidateEvents.Object,
            incidents.Object,
            state.Object);

        var result = await service.ConsumeAsync(candidateEvent);

        Assert.False(result.Decision.CreatesIncident);
        Assert.Equal("CO_PRESENCE_NOT_FOUND", result.Decision.ReasonCode.Value);
        Assert.Null(result.Incident);
        incidents.Verify(x => x.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldCreateIncident_WhenAggressorAndProtectedWomanShareCameraContext()
    {
        var protectedWomanCase = new ProtectedCase(
            new ExternalCaseId("case-victim"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);
        var aggressorCase = new ProtectedCase(
            new ExternalCaseId("case-aggressor"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.Aggressor);

        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        var candidateEvent = new BiometricCandidateEvent(
            aggressorCase.Id,
            scope,
            new MatchScore(0.92),
            DateTime.UtcNow);
        var priorVictimEvent = new BiometricCandidateEvent(
            protectedWomanCase.Id,
            scope,
            new MatchScore(0.89),
            candidateEvent.OccurredAtUtc.AddMinutes(-1));

        var rule = new MonitoringRule(
            aggressorCase.Id,
            scope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true);
        var counterpartRule = new MonitoringRule(
            protectedWomanCase.Id,
            scope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true);

        var cases = new Mock<ICaseProjectionRepository>();
        cases.Setup(x => x.GetByIdAsync(aggressorCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aggressorCase);
        cases.Setup(x => x.GetByIdAsync(protectedWomanCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(protectedWomanCase);

        var rules = new Mock<IMonitoringRuleRepository>();
        rules.Setup(x => x.ListByCaseAsync(aggressorCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([rule]);
        rules.Setup(x => x.ListByCaseAsync(protectedWomanCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([counterpartRule]);

        var candidateEvents = new Mock<ICandidateEventRepository>();
        candidateEvents.Setup(x => x.GetByIdAsync(candidateEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync((BiometricCandidateEvent?)null);
        candidateEvents.Setup(x => x.ListRecentByCameraScopeAsync(
                scope,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidateEvent, priorVictimEvent]);

        var incidents = new Mock<IIncidentRepository>();
        incidents.Setup(x => x.FindLatestActiveByCaseAndCameraScopeAsync(
                protectedWomanCase.Id,
                scope,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Incident?)null);

        var state = new Mock<IShortLivedStatePort>();
        state.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var service = CreateService(
            cases.Object,
            rules.Object,
            candidateEvents.Object,
            incidents.Object,
            state.Object);

        var result = await service.ConsumeAsync(candidateEvent);

        Assert.True(result.Decision.CreatesIncident);
        Assert.Equal("CO_PRESENCE_MATCH", result.Decision.ReasonCode.Value);
        Assert.NotNull(result.Incident);
        Assert.Equal(protectedWomanCase.Id, result.Incident!.ProtectedCaseId);
        incidents.Verify(x => x.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldSuppressEncounter_WhenActiveIncidentAlreadyExistsForProtectedWomanAndCamera()
    {
        var protectedWomanCase = new ProtectedCase(
            new ExternalCaseId("case-victim"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.ProtectedWoman);
        var aggressorCase = new ProtectedCase(
            new ExternalCaseId("case-aggressor"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted,
            MonitoredSubjectRole.Aggressor);

        var scope = new CameraScope(Guid.NewGuid(), Guid.NewGuid());
        var candidateEvent = new BiometricCandidateEvent(
            aggressorCase.Id,
            scope,
            new MatchScore(0.92),
            DateTime.UtcNow);
        var priorVictimEvent = new BiometricCandidateEvent(
            protectedWomanCase.Id,
            scope,
            new MatchScore(0.89),
            candidateEvent.OccurredAtUtc.AddMinutes(-1));

        var aggressorRule = new MonitoringRule(
            aggressorCase.Id,
            scope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true);
        var victimRule = new MonitoringRule(
            protectedWomanCase.Id,
            scope,
            new TimeWindow(TimeOnly.MinValue, new TimeOnly(23, 59)),
            true);

        var cases = new Mock<ICaseProjectionRepository>();
        cases.Setup(x => x.GetByIdAsync(aggressorCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(aggressorCase);
        cases.Setup(x => x.GetByIdAsync(protectedWomanCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(protectedWomanCase);

        var rules = new Mock<IMonitoringRuleRepository>();
        rules.Setup(x => x.ListByCaseAsync(aggressorCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([aggressorRule]);
        rules.Setup(x => x.ListByCaseAsync(protectedWomanCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([victimRule]);

        var candidateEvents = new Mock<ICandidateEventRepository>();
        candidateEvents.Setup(x => x.GetByIdAsync(candidateEvent.Id, It.IsAny<CancellationToken>())).ReturnsAsync((BiometricCandidateEvent?)null);
        candidateEvents.Setup(x => x.ListRecentByCameraScopeAsync(
                scope,
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([candidateEvent, priorVictimEvent]);

        var incidents = new Mock<IIncidentRepository>();
        incidents.Setup(x => x.FindLatestActiveByCaseAndCameraScopeAsync(
                protectedWomanCase.Id,
                scope,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Incident(protectedWomanCase.Id, Guid.NewGuid()));

        var state = new Mock<IShortLivedStatePort>();
        state.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var service = CreateService(
            cases.Object,
            rules.Object,
            candidateEvents.Object,
            incidents.Object,
            state.Object);

        var result = await service.ConsumeAsync(candidateEvent);

        Assert.False(result.Decision.CreatesIncident);
        Assert.Equal("ENCOUNTER_ALREADY_OPEN", result.Decision.ReasonCode.Value);
        incidents.Verify(x => x.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CandidateEventCorrelationService CreateService(
        ICaseProjectionRepository caseProjectionRepository,
        IMonitoringRuleRepository monitoringRuleRepository,
        ICandidateEventRepository candidateEventRepository,
        IIncidentRepository incidentRepository,
        IShortLivedStatePort shortLivedStatePort)
    {
        var decisions = new Mock<ICorrelationDecisionRepository>();
        var audits = new Mock<IAuditLogRepository>();
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);

        return new CandidateEventCorrelationService(
            caseProjectionRepository,
            monitoringRuleRepository,
            candidateEventRepository,
            decisions.Object,
            incidentRepository,
            audits.Object,
            shortLivedStatePort,
            clock.Object,
            new CorrelationEngineOptions());
    }
}

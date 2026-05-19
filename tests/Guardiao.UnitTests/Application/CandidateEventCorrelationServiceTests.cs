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
    public async Task ConsumeAsync_ShouldCreateIncident_WhenRuleMatchesAndCaseIsEnabled()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-1"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

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

        var incidents = new Mock<IIncidentRepository>();
        incidents.Setup(x => x.FindLatestActiveByCaseAsync(protectedCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Incident?)null);

        var state = new Mock<IShortLivedStatePort>();
        state.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        var service = CreateService(
            cases.Object,
            rules.Object,
            incidents.Object,
            state.Object);

        var result = await service.ConsumeAsync(candidateEvent);

        Assert.True(result.Decision.CreatesIncident);
        Assert.Equal("RULE_MATCH", result.Decision.ReasonCode.Value);
        Assert.NotNull(result.Incident);
        incidents.Verify(x => x.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldSuppressDuplicate_WhenShortLivedKeyAlreadyExists()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-1"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);

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

        var incidents = new Mock<IIncidentRepository>();
        var state = new Mock<IShortLivedStatePort>();
        state.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("existing");

        var service = CreateService(
            cases.Object,
            rules.Object,
            incidents.Object,
            state.Object);

        var result = await service.ConsumeAsync(candidateEvent);

        Assert.False(result.Decision.CreatesIncident);
        Assert.Equal("DUPLICATE_SUPPRESSED", result.Decision.ReasonCode.Value);
        incidents.Verify(x => x.AddAsync(It.IsAny<Incident>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CandidateEventCorrelationService CreateService(
        ICaseProjectionRepository caseProjectionRepository,
        IMonitoringRuleRepository monitoringRuleRepository,
        IIncidentRepository incidentRepository,
        IShortLivedStatePort shortLivedStatePort)
    {
        var candidateEvents = new Mock<ICandidateEventRepository>();
        var decisions = new Mock<ICorrelationDecisionRepository>();
        var notifications = new Mock<INotificationPort>();
        var audits = new Mock<IAuditLogRepository>();
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTime.UtcNow);

        return new CandidateEventCorrelationService(
            caseProjectionRepository,
            monitoringRuleRepository,
            candidateEvents.Object,
            decisions.Object,
            incidentRepository,
            notifications.Object,
            audits.Object,
            shortLivedStatePort,
            clock.Object,
            new CorrelationEngineOptions());
    }
}

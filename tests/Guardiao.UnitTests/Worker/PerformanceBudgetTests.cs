using System.Diagnostics;
using System.Text.Json;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Application.Services;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.System;
using Guardiao.Worker.Edge.Adapters;
using Guardiao.Worker.Edge.Options;
using Guardiao.Worker.Edge.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Guardiao.UnitTests.Worker;

[Trait("Category", "Performance")]
public class PerformanceBudgetTests
{
    [Fact]
    public async Task StageBudgets_ShouldRemainWithinSmokeThresholds()
    {
        var metrics = new EdgeMetricsCollector();
        var options = new EdgeWorkerOptions
        {
            MatchThreshold = 0.1,
            MinimumDetectionScore = 0.1,
            RestrictedGallery =
            [
                new RestrictedGallerySeedOptions
                {
                    ProtectedCaseId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    SiteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    PersonProjectionId = Guid.NewGuid(),
                    ExternalPersonId = "person-budget-1",
                    IsBystander = false,
                    Embedding = Enumerable.Repeat(0.25f, 16).ToArray()
                }
            ]
        };

        var capturePort = new StubCameraCapturePort();
        var detector = new DeterministicFaceDetectorPort(Options.Create(options));
        var tracker = new DeterministicFaceTrackerPort();
        var embedder = new DeterministicFaceEmbedderPort();
        var galleryProvider = new RestrictedGalleryProvider(Options.Create(options));
        var matcher = new RestrictedGalleryMatcherPort(galleryProvider, metrics, Options.Create(options));

        var camera = new Camera(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "PerfCam", "webcam://0");
        var trackedFace = new TrackedFace(Guid.NewGuid(), [1, 2, 3, 4]);
        var candidateEvent = new BiometricCandidateEvent(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            new CameraScope(camera.SiteId, Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            new MatchScore(0.91),
            DateTime.UtcNow);

        var capture = await ProbeAsync("capture", 20, async () =>
        {
            await using var stream = await capturePort.CaptureFrameAsync(camera);
            _ = ((MemoryStream)stream).Length;
        }, budgetP95Ms: 5);

        var detect = await ProbeAsync("detect", 20, async () =>
        {
            await using var stream = new MemoryStream([1, 2, 3, 4]);
            _ = await detector.DetectAsync(stream);
        }, budgetP95Ms: 5);

        var embed = await ProbeAsync("embed", 20, async () =>
        {
            _ = await embedder.CreateEmbeddingAsync(trackedFace);
        }, budgetP95Ms: 5);

        var match = await ProbeAsync("match", 20, async () =>
        {
            var embedding = await embedder.CreateEmbeddingAsync(trackedFace);
            _ = await matcher.MatchAsync(embedding, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        }, budgetP95Ms: 5);

        var correlation = await ProbeAsync("correlation", 20, async () =>
        {
            var service = CreateCorrelationService();
            _ = await service.ConsumeAsync(candidateEvent);
        }, budgetP95Ms: 20);

        var report = new PerformanceBudgetReport(new[] { capture, detect, embed, match, correlation });
        var outputPath = PerformanceArtifactWriter.Write("performance-budget-smoke", report);

        Assert.All(report.Stages, stage => Assert.True(stage.WithinBudget, $"{stage.StageName} exceeded budget."));
        Assert.True(File.Exists(outputPath));
    }

    private static CandidateEventCorrelationService CreateCorrelationService()
    {
        var protectedCase = new ProtectedCase(
            new ExternalCaseId("case-budget"),
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            MonitoringStatus.Enabled,
            ConsentStatus.Granted);
        var rule = new MonitoringRule(
            protectedCase.Id,
            new CameraScope(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            new TimeWindow(new TimeOnly(0, 0), new TimeOnly(23, 59)),
            true);

        var caseRepo = new Mock<ICaseProjectionRepository>();
        caseRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(protectedCase);

        var ruleRepo = new Mock<IMonitoringRuleRepository>();
        ruleRepo.Setup(x => x.ListByCaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([rule]);

        var candidateRepo = new Mock<ICandidateEventRepository>();
        candidateRepo.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BiometricCandidateEvent?)null);
        var decisionRepo = new Mock<ICorrelationDecisionRepository>();
        var incidentRepo = new Mock<IIncidentRepository>();
        incidentRepo.Setup(x => x.FindLatestActiveByCaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Incident?)null);

        var audit = new Mock<IAuditLogRepository>();
        var shortLivedState = new Mock<IShortLivedStatePort>();
        shortLivedState.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        return new CandidateEventCorrelationService(
            caseRepo.Object,
            ruleRepo.Object,
            candidateRepo.Object,
            decisionRepo.Object,
            incidentRepo.Object,
            audit.Object,
            shortLivedState.Object,
            new SystemClock(),
            new CorrelationEngineOptions
            {
                CooldownWindow = TimeSpan.FromMinutes(5),
                DuplicateSuppressionWindow = TimeSpan.FromSeconds(30)
            });
    }

    private static async Task<PerformanceStageResult> ProbeAsync(string stageName, int iterations, Func<Task> action, double budgetP95Ms)
    {
        var samples = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p95Index = Math.Max(0, (int)Math.Ceiling(samples.Count * 0.95) - 1);
        var p95 = samples[p95Index];
        return new PerformanceStageResult(stageName, budgetP95Ms, p95, p95 <= budgetP95Ms);
    }
}

file sealed class StubCameraCapturePort : ICameraCapturePort
{
    private static readonly byte[] FrameBytes = [1, 2, 3, 4];

    public Task<Stream> CaptureFrameAsync(Camera camera, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new MemoryStream(FrameBytes, writable: false));
    }
}

public sealed record PerformanceStageResult(string StageName, double BudgetP95Ms, double ActualP95Ms, bool WithinBudget);

public sealed record PerformanceBudgetReport(IReadOnlyCollection<PerformanceStageResult> Stages);

public static class PerformanceArtifactWriter
{
    public static string Write(string name, object payload)
    {
        var directory = Path.Combine(Path.GetTempPath(), "guardiao-verification");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{name}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        return path;
    }
}

using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Domain.Enums;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/operations")]
[Authorize(Policy = AuthorizationPolicies.MetadataRead)]
public class OperationsController : ControllerBase
{
    private readonly GuardiaoDbContext _dbContext;
    private readonly ICameraLivePreviewPort _cameraLivePreviewPort;

    public OperationsController(GuardiaoDbContext dbContext, ICameraLivePreviewPort cameraLivePreviewPort)
    {
        _dbContext = dbContext;
        _cameraLivePreviewPort = cameraLivePreviewPort;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var incidentCount = await _dbContext.Incidents.CountAsync(cancellationToken);
        var caseCount = await _dbContext.ProtectedCases.CountAsync(cancellationToken);
        var cameraCount = await _dbContext.Cameras.CountAsync(cancellationToken);
        var auditEntryCount = await _dbContext.AuditLogs.CountAsync(cancellationToken);

        var recentIncidents = await _dbContext.Incidents
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new RecentIncidentResponse(
                x.Id,
                x.ProtectedCaseId,
                x.Status.ToString(),
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var recentAuditEntries = await _dbContext.AuditLogs
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .Select(x => new AuditEntryResponse(
                x.Id,
                x.ActorType.ToString(),
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var cameras = await (
            from camera in _dbContext.Cameras
            join site in _dbContext.Sites on camera.SiteId equals site.Id
            orderby site.Name, camera.Name
            select new CameraDescriptor(
                camera.Id,
                camera.SiteId,
                camera.Name,
                site.Name,
                camera.IsEnabled,
                camera.StreamEndpoint))
            .ToListAsync(cancellationToken);

        var recentDetections = await LoadRecentDetectionsAsync(cancellationToken);
        var cameraViews = BuildCameraViews(cameras, recentDetections);

        return Ok(new OperationsSummaryResponse(
            incidentCount,
            caseCount,
            cameraCount,
            auditEntryCount,
            recentIncidents,
            recentAuditEntries,
            cameraViews));
    }

    [HttpGet("cameras/{cameraId:guid}/preview")]
    public async Task<IActionResult> GetCameraPreview(Guid cameraId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Cameras.AnyAsync(x => x.Id == cameraId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var preview = await _cameraLivePreviewPort.GetLatestPreviewAsync(cameraId, cancellationToken);
        if (preview is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        if (preview.CapturedAtUtc.HasValue)
        {
            Response.Headers.Append("X-Captured-At-Utc", preview.CapturedAtUtc.Value.ToString("O"));
        }
        if (preview.Sequence.HasValue)
        {
            Response.Headers.Append("X-Frame-Sequence", preview.Sequence.Value.ToString());
        }

        return File(preview.Content, preview.ContentType);
    }

    [HttpGet("cameras/{cameraId:guid}/live")]
    public async Task<IActionResult> GetCameraLiveStream(Guid cameraId, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.Cameras.AnyAsync(x => x.Id == cameraId, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        await using var livePreview = await _cameraLivePreviewPort.OpenLivePreviewStreamAsync(cameraId, cancellationToken);
        if (livePreview is null)
        {
            return NotFound();
        }

        Response.ContentType = livePreview.ContentType;
        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        await livePreview.Content.CopyToAsync(Response.Body, cancellationToken);
        return new EmptyResult();
    }

    private async Task<List<RecentDetectionRow>> LoadRecentDetectionsAsync(CancellationToken cancellationToken)
    {
        var recentCutoffUtc = DateTime.UtcNow.AddHours(-12);

        return await (
            from candidate in _dbContext.BiometricCandidateEvents
            join protectedCase in _dbContext.ProtectedCases on candidate.ProtectedCaseId equals protectedCase.Id
            join person in _dbContext.PersonProjections on protectedCase.PersonProjectionId equals person.Id
            join camera in _dbContext.Cameras on candidate.CameraScope.CameraId equals camera.Id
            join site in _dbContext.Sites on candidate.CameraScope.SiteId equals site.Id
            where candidate.OccurredAtUtc >= recentCutoffUtc
            orderby candidate.OccurredAtUtc descending
            select new RecentDetectionRow(
                candidate.Id,
                protectedCase.Id,
                protectedCase.SubjectRole,
                person.Id,
                person.FullName,
                person.IsBystander,
                site.Id,
                site.Name,
                camera.Id,
                camera.Name,
                candidate.MatchScore.Value,
                candidate.OccurredAtUtc,
                _dbContext.Incidents
                    .Where(x => x.CandidateEventId == candidate.Id)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefault(),
                _dbContext.Incidents
                    .Where(x => x.CandidateEventId == candidate.Id)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (IncidentStatus?)x.Status)
                    .FirstOrDefault(),
                _dbContext.EvidenceArtifacts
                    .Where(x => x.CandidateEventId == candidate.Id && x.ArtifactType == EvidenceArtifactType.Snapshot)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (Guid?)x.Id)
                    .FirstOrDefault(),
                _dbContext.EvidenceArtifacts
                    .Where(x => x.CandidateEventId == candidate.Id && x.ArtifactType == EvidenceArtifactType.Snapshot)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (Guid?)x.IncidentId)
                    .FirstOrDefault(),
                _dbContext.EvidenceArtifacts
                    .Where(x => x.CandidateEventId == candidate.Id && x.ArtifactType == EvidenceArtifactType.Snapshot)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => x.ContentType)
                    .FirstOrDefault(),
                _dbContext.EvidenceArtifacts
                    .Where(x => x.CandidateEventId == candidate.Id && x.ArtifactType == EvidenceArtifactType.Snapshot)
                    .OrderByDescending(x => x.CreatedAtUtc)
                    .Select(x => (DateTime?)x.CreatedAtUtc)
                    .FirstOrDefault()))
            .Take(40)
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyCollection<CameraOperationalViewResponse> BuildCameraViews(
        IReadOnlyCollection<CameraDescriptor> cameras,
        IReadOnlyCollection<RecentDetectionRow> recentDetections)
    {
        var cameraViews = new List<CameraOperationalViewResponse>(cameras.Count);

        foreach (var camera in cameras)
        {
            var detections = recentDetections
                .Where(x => x.CameraId == camera.Id)
                .OrderByDescending(x => x.DetectedAtUtc)
                .ToList();

            var latestDetection = detections.FirstOrDefault();
            var latestSnapshot = detections.FirstOrDefault(x => HasSnapshot(x));
            var protectedWomen = detections
                .Where(IsProtectedWomanDetection)
                .Take(4)
                .Select(ToDetectedSubjectResponse)
                .ToArray();

            cameraViews.Add(new CameraOperationalViewResponse(
                camera.Id,
                camera.SiteId,
                camera.Name,
                camera.SiteName,
                camera.IsEnabled,
                camera.StreamEndpoint,
                latestDetection?.DetectedAtUtc,
                latestSnapshot is null ? null : ToEvidencePreview(latestSnapshot),
                protectedWomen,
                BuildAggressorAlert(detections)));
        }

        return cameraViews;
    }

    private static AggressorPresenceAlertResponse? BuildAggressorAlert(IReadOnlyCollection<RecentDetectionRow> detections)
    {
        var protectedWomen = detections
            .Where(IsProtectedWomanDetection)
            .ToList();
        if (protectedWomen.Count == 0)
        {
            return null;
        }

        var alertWindow = TimeSpan.FromMinutes(10);
        var aggressorDetection = detections
            .Where(x => x.SubjectRole == MonitoredSubjectRole.Aggressor)
            .FirstOrDefault(x => protectedWomen.Any(y => Math.Abs((x.DetectedAtUtc - y.DetectedAtUtc).TotalMinutes) <= alertWindow.TotalMinutes));
        if (aggressorDetection is null)
        {
            return null;
        }

        var nearbyProtectedWomen = protectedWomen
            .Where(x => Math.Abs((aggressorDetection.DetectedAtUtc - x.DetectedAtUtc).TotalMinutes) <= alertWindow.TotalMinutes)
            .Select(x => x.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AggressorPresenceAlertResponse(
            aggressorDetection.ProtectedCaseId,
            aggressorDetection.FullName,
            aggressorDetection.MatchScore,
            aggressorDetection.DetectedAtUtc,
            nearbyProtectedWomen,
            ToEvidencePreview(aggressorDetection));
    }

    private static bool IsProtectedWomanDetection(RecentDetectionRow detection)
        => detection.SubjectRole == MonitoredSubjectRole.ProtectedWoman && !detection.IsBystander;

    private static DetectedSubjectResponse ToDetectedSubjectResponse(RecentDetectionRow detection)
    {
        return new DetectedSubjectResponse(
            detection.ProtectedCaseId,
            detection.PersonProjectionId,
            detection.FullName,
            detection.SubjectRole.ToString(),
            detection.IsBystander,
            detection.IncidentStatus?.ToString() ?? "PendingReview",
            detection.MatchScore,
            detection.DetectedAtUtc,
            ToEvidencePreview(detection));
    }

    private static EvidencePreviewResponse? ToEvidencePreview(RecentDetectionRow detection)
    {
        if (!HasSnapshot(detection))
        {
            return null;
        }

        return new EvidencePreviewResponse(
            detection.SnapshotIncidentId!.Value,
            detection.SnapshotEvidenceId!.Value,
            detection.SnapshotContentType!,
            detection.SnapshotCreatedAtUtc!.Value);
    }

    private static bool HasSnapshot(RecentDetectionRow detection)
    {
        return detection.SnapshotEvidenceId.HasValue &&
               detection.SnapshotIncidentId.HasValue &&
               detection.SnapshotCreatedAtUtc.HasValue &&
               !string.IsNullOrWhiteSpace(detection.SnapshotContentType);
    }

    private sealed record CameraDescriptor(
        Guid Id,
        Guid SiteId,
        string Name,
        string SiteName,
        bool IsEnabled,
        string StreamEndpoint);

    private sealed record RecentDetectionRow(
        Guid CandidateEventId,
        Guid ProtectedCaseId,
        MonitoredSubjectRole SubjectRole,
        Guid PersonProjectionId,
        string FullName,
        bool IsBystander,
        Guid SiteId,
        string SiteName,
        Guid CameraId,
        string CameraName,
        double MatchScore,
        DateTime DetectedAtUtc,
        Guid? IncidentId,
        IncidentStatus? IncidentStatus,
        Guid? SnapshotEvidenceId,
        Guid? SnapshotIncidentId,
        string? SnapshotContentType,
        DateTime? SnapshotCreatedAtUtc);
}

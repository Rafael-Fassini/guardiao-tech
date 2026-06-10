using Guardiao.Api.Contracts;
using Guardiao.Api.Infrastructure;
using Guardiao.Application.Ports.Outbound;
using Guardiao.Domain.Entities;
using Guardiao.Domain.Enums;
using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Guardiao.Api.Controllers;

[ApiController]
[Route("api/cases/{caseId:guid}/biometrics")]
[Authorize(Policy = AuthorizationPolicies.CasesRead)]
public sealed class CaseBiometricsController : ControllerBase
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private readonly GuardiaoDbContext _dbContext;
    private readonly IBiometricTemplateExtractor _extractor;
    private readonly IEvidenceStoragePort _storage;

    public CaseBiometricsController(
        GuardiaoDbContext dbContext,
        IBiometricTemplateExtractor extractor,
        IEvidenceStoragePort storage)
    {
        _dbContext = dbContext;
        _extractor = extractor;
        _storage = storage;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid caseId, CancellationToken cancellationToken)
    {
        var protectedCase = await _dbContext.ProtectedCases
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (protectedCase is null)
        {
            return NotFound();
        }

        var templates = await _dbContext.BiometricTemplates
            .Where(x => x.PersonProjectionId == protectedCase.PersonProjectionId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BiometricTemplateResponse(
                x.Id,
                x.PersonProjectionId,
                x.ExternalPersonId.Value,
                x.Source,
                x.DisplayName,
                x.ContentType,
                x.IsActive,
                x.CreatedAt,
                x.DeactivatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(templates);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid caseId, IFormFile? file, CancellationToken cancellationToken)
    {
        var protectedCase = await _dbContext.ProtectedCases
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (protectedCase is null)
        {
            return NotFound();
        }

        var personProjection = await _dbContext.PersonProjections
            .FirstOrDefaultAsync(x => x.Id == protectedCase.PersonProjectionId, cancellationToken);
        if (personProjection is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return ValidationProblemResponse("file", "A biometric image file is required.");
        }

        if (!AllowedImageContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationProblemResponse("file", "Unsupported image content type.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationProblemResponse("file", "Unsupported image file extension.");
        }

        await using var sourceStream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        BiometricExtractionResult extraction;
        try
        {
            await using var extractionStream = new MemoryStream(bytes, writable: false);
            extraction = await _extractor.ExtractAsync(extractionStream, cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            return ValidationProblemResponse("file", ex.Message);
        }

        var safeDisplayName = CreateSafeDisplayName(file.FileName);
        await using var storageStream = new MemoryStream(bytes, writable: false);
        var storagePath = await _storage.StoreAsync(storageStream, safeDisplayName, file.ContentType, cancellationToken);

        var template = new BiometricTemplate(
            personProjection.Id,
            personProjection.ExternalPersonId,
            extraction.Embedding,
            RetentionMode.CaseBound,
            personProjection.IsBystander,
            "panel_upload",
            safeDisplayName,
            file.ContentType,
            storagePath);

        _dbContext.BiometricTemplates.Add(template);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/cases/{caseId}/biometrics/{template.Id}",
            new BiometricTemplateUploadResponse(
                template.Id,
                template.PersonProjectionId,
                template.ExternalPersonId.Value,
                template.DisplayName,
                template.ContentType,
                template.CreatedAt));
    }

    [HttpDelete("{templateId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.RulesManage)]
    [EnableRateLimiting(SecurityRateLimitPolicies.ApiWrites)]
    public async Task<IActionResult> Deactivate(Guid caseId, Guid templateId, CancellationToken cancellationToken)
    {
        var protectedCase = await _dbContext.ProtectedCases
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (protectedCase is null)
        {
            return NotFound();
        }

        var template = await _dbContext.BiometricTemplates
            .FirstOrDefaultAsync(x => x.Id == templateId && x.PersonProjectionId == protectedCase.PersonProjectionId, cancellationToken);
        if (template is null)
        {
            return NotFound();
        }

        template.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("/api/cases/{caseId:guid}/gallery")]
    [Authorize(Policy = AuthorizationPolicies.BiometricGalleryRead)]
    public async Task<IActionResult> GetGallery(Guid caseId, [FromQuery] Guid siteId, CancellationToken cancellationToken)
    {
        var protectedCase = await _dbContext.ProtectedCases
            .FirstOrDefaultAsync(x => x.Id == caseId, cancellationToken);
        if (protectedCase is null)
        {
            return NotFound();
        }

        var personProjection = await _dbContext.PersonProjections
            .FirstOrDefaultAsync(x => x.Id == protectedCase.PersonProjectionId, cancellationToken);
        if (personProjection is null)
        {
            return NotFound();
        }

        var templates = await _dbContext.BiometricTemplates
            .Where(x => x.PersonProjectionId == protectedCase.PersonProjectionId && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BiometricGalleryEntryResponse(
                protectedCase.Id,
                siteId,
                x.PersonProjectionId,
                x.ExternalPersonId.Value,
                personProjection.IsBystander,
                x.Embedding))
            .ToListAsync(cancellationToken);

        return Ok(templates);
    }

    private IActionResult ValidationProblemResponse(string key, string message)
    {
        ModelState.AddModelError(key, message);
        return ValidationProblem(ModelState);
    }

    private static string CreateSafeDisplayName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);
        var sanitized = new string(baseName.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "biometric";
        }

        if (sanitized.Length > 48)
        {
            sanitized = sanitized[..48];
        }

        return $"{sanitized}{extension}";
    }
}

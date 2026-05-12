using System.ComponentModel.DataAnnotations;

namespace Guardiao.Api.Contracts;

public class UpdateMonitoringRuleRequest
{
    [Required]
    public Guid SiteId { get; set; }

    [Required]
    public Guid CameraId { get; set; }

    [Required]
    public TimeOnly StartsAt { get; set; }

    [Required]
    public TimeOnly EndsAt { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public class IncidentReviewRequest
{
    [MaxLength(500)]
    public string ReviewNotes { get; set; } = string.Empty;
}

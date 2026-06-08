using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class HousekeepingOptions
{
    public const string SectionName = "Housekeeping";

    public bool EnableEvidenceEligibilityScan { get; set; } = true;
    public int EvidenceEligibilityScanIntervalSeconds { get; set; } = 900;
}

public sealed class HousekeepingOptionsValidator : IValidateOptions<HousekeepingOptions>
{
    public ValidateOptionsResult Validate(string? name, HousekeepingOptions options)
    {
        var errors = new List<string>();

        if (options.EvidenceEligibilityScanIntervalSeconds <= 0)
        {
            errors.Add("Housekeeping:EvidenceEligibilityScanIntervalSeconds must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

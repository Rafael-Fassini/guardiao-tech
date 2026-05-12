using Guardiao.Domain.Exceptions;

namespace Guardiao.Domain.ValueObjects;

public readonly record struct ExternalCaseId
{
    public string Value { get; }

    public ExternalCaseId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("External case id is required.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public readonly record struct ExternalPersonId
{
    public string Value { get; }

    public ExternalPersonId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("External person id is required.");
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}

public readonly record struct MonitoringStatus
{
    public string Value { get; }

    public MonitoringStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("Monitoring status must be explicit.");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is not ("enabled" or "disabled" or "suspended" or "revoked"))
        {
            throw new InvariantViolationException($"Unsupported monitoring status '{value}'.");
        }

        Value = normalized;
    }

    public bool IsEnabled => Value == "enabled";

    public static MonitoringStatus Enabled => new("enabled");
    public static MonitoringStatus Disabled => new("disabled");
    public static MonitoringStatus Suspended => new("suspended");
    public static MonitoringStatus Revoked => new("revoked");

    public override string ToString() => Value;
}

public readonly record struct ConsentStatus
{
    public string Value { get; }

    public ConsentStatus(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("Consent status must be explicit.");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is not ("granted" or "pending" or "denied" or "not_required"))
        {
            throw new InvariantViolationException($"Unsupported consent status '{value}'.");
        }

        Value = normalized;
    }

    public bool AllowsBiometricPersistence => Value is "granted" or "not_required";

    public static ConsentStatus Granted => new("granted");
    public static ConsentStatus Pending => new("pending");
    public static ConsentStatus Denied => new("denied");
    public static ConsentStatus NotRequired => new("not_required");

    public override string ToString() => Value;
}

public readonly record struct MatchScore
{
    public double Value { get; }

    public MatchScore(double value)
    {
        if (value is < 0 or > 1)
        {
            throw new InvariantViolationException("Match score must be between 0 and 1.");
        }

        Value = value;
    }

    public override string ToString() => Value.ToString("0.000");
}

public readonly record struct TimeWindow
{
    public TimeOnly StartsAt { get; }
    public TimeOnly EndsAt { get; }

    public TimeWindow(TimeOnly startsAt, TimeOnly endsAt)
    {
        if (startsAt == endsAt)
        {
            throw new InvariantViolationException("Time window must have distinct boundaries.");
        }

        StartsAt = startsAt;
        EndsAt = endsAt;
    }

    public bool Contains(TimeOnly value)
    {
        return StartsAt < EndsAt
            ? value >= StartsAt && value <= EndsAt
            : value >= StartsAt || value <= EndsAt;
    }
}

public readonly record struct CameraScope
{
    public Guid SiteId { get; }
    public Guid CameraId { get; }

    public CameraScope(Guid siteId, Guid cameraId)
    {
        if (siteId == Guid.Empty)
        {
            throw new InvariantViolationException("Camera scope requires a valid site id.");
        }

        if (cameraId == Guid.Empty)
        {
            throw new InvariantViolationException("Camera scope requires a valid camera id.");
        }

        SiteId = siteId;
        CameraId = cameraId;
    }
}

public readonly record struct CorrelationReasonCode
{
    public string Value { get; }

    public CorrelationReasonCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("Correlation reason code is required.");
        }

        Value = value.Trim().ToUpperInvariant();
    }

    public override string ToString() => Value;
}

public readonly record struct RetentionMode
{
    public string Value { get; }

    public RetentionMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvariantViolationException("Retention mode is required.");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is not ("short_lived" or "case_bound" or "audit_only"))
        {
            throw new InvariantViolationException($"Unsupported retention mode '{value}'.");
        }

        Value = normalized;
    }

    public static RetentionMode ShortLived => new("short_lived");
    public static RetentionMode CaseBound => new("case_bound");
    public static RetentionMode AuditOnly => new("audit_only");

    public override string ToString() => Value;
}

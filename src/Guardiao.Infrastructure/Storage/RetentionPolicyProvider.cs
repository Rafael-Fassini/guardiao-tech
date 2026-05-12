using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Storage;

public interface IRetentionPolicyProvider
{
    TimeSpan GetRetentionWindow(RetentionMode retentionMode);
}

public sealed class RetentionPolicyProvider : IRetentionPolicyProvider
{
    private readonly RetentionOptions _options;

    public RetentionPolicyProvider(IOptions<RetentionOptions> options)
    {
        _options = options.Value;
    }

    public TimeSpan GetRetentionWindow(RetentionMode retentionMode)
    {
        return retentionMode.Value switch
        {
            "short_lived" => TimeSpan.FromDays(_options.ShortLivedDays),
            "case_bound" => TimeSpan.FromDays(_options.CaseBoundDays),
            "audit_only" => TimeSpan.FromDays(_options.AuditOnlyDays),
            _ => throw new InvalidOperationException($"Unsupported retention mode '{retentionMode.Value}'.")
        };
    }
}

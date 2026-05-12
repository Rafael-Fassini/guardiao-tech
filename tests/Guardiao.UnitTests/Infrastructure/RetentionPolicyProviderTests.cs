using Guardiao.Domain.ValueObjects;
using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class RetentionPolicyProviderTests
{
    [Fact]
    public void GetRetentionWindow_ShouldUseConfiguredDurations()
    {
        var provider = new RetentionPolicyProvider(Options.Create(new RetentionOptions
        {
            ShortLivedDays = 2,
            CaseBoundDays = 30,
            AuditOnlyDays = 365
        }));

        Assert.Equal(TimeSpan.FromDays(2), provider.GetRetentionWindow(RetentionMode.ShortLived));
        Assert.Equal(TimeSpan.FromDays(30), provider.GetRetentionWindow(RetentionMode.CaseBound));
        Assert.Equal(TimeSpan.FromDays(365), provider.GetRetentionWindow(RetentionMode.AuditOnly));
    }
}

using Guardiao.Infrastructure.Options;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class OperationalNotificationsOptionsValidatorTests
{
    [Fact]
    public void Validate_ShouldFail_WhenWebhookIsEnabledWithoutValidUri()
    {
        var validator = new OperationalNotificationsOptionsValidator();

        var result = validator.Validate(null, new OperationalNotificationsOptions
        {
            EnableWebhook = true,
            WebhookUrl = "not-a-uri"
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("WebhookUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSmtpIsEnabledWithoutRecipients()
    {
        var validator = new OperationalNotificationsOptionsValidator();

        var result = validator.Validate(null, new OperationalNotificationsOptions
        {
            EnableSmtp = true,
            SmtpHost = "localhost",
            SmtpPort = 1025,
            SmtpSender = "guardiao@example.test"
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, x => x.Contains("SmtpRecipients", StringComparison.Ordinal));
    }
}

using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public sealed class OperationalNotificationsOptions
{
    public const string SectionName = "OperationalNotifications";

    public bool EnableWebhook { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public bool EnableSmtp { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 25;
    public bool SmtpUseSsl { get; set; }
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string SmtpSender { get; set; } = string.Empty;
    public string[] SmtpRecipients { get; set; } = [];
    public int DeliveryTimeoutSeconds { get; set; } = 5;
    public int RetryAttempts { get; set; } = 3;
    public int InitialRetryDelayMilliseconds { get; set; } = 250;
    public bool EnableEscalation { get; set; } = true;
    public int EscalationWindowMinutes { get; set; } = 15;
    public int EscalationScanIntervalSeconds { get; set; } = 60;
}

public sealed class OperationalNotificationsOptionsValidator : IValidateOptions<OperationalNotificationsOptions>
{
    public ValidateOptionsResult Validate(string? name, OperationalNotificationsOptions options)
    {
        var errors = new List<string>();

        if (options.EnableWebhook && !Uri.TryCreate(options.WebhookUrl, UriKind.Absolute, out _))
        {
            errors.Add("OperationalNotifications:WebhookUrl must be a valid absolute URI when webhook notifications are enabled.");
        }

        if (options.EnableSmtp)
        {
            if (string.IsNullOrWhiteSpace(options.SmtpHost))
            {
                errors.Add("OperationalNotifications:SmtpHost must be configured when SMTP notifications are enabled.");
            }

            if (options.SmtpPort <= 0)
            {
                errors.Add("OperationalNotifications:SmtpPort must be greater than zero when SMTP notifications are enabled.");
            }

            if (string.IsNullOrWhiteSpace(options.SmtpSender))
            {
                errors.Add("OperationalNotifications:SmtpSender must be configured when SMTP notifications are enabled.");
            }

            if (options.SmtpRecipients.Length == 0 || options.SmtpRecipients.All(string.IsNullOrWhiteSpace))
            {
                errors.Add("OperationalNotifications:SmtpRecipients must contain at least one destination when SMTP notifications are enabled.");
            }
        }

        if (options.DeliveryTimeoutSeconds <= 0)
        {
            errors.Add("OperationalNotifications:DeliveryTimeoutSeconds must be greater than zero.");
        }

        if (options.RetryAttempts <= 0)
        {
            errors.Add("OperationalNotifications:RetryAttempts must be greater than zero.");
        }

        if (options.InitialRetryDelayMilliseconds < 0)
        {
            errors.Add("OperationalNotifications:InitialRetryDelayMilliseconds cannot be negative.");
        }

        if (options.EscalationWindowMinutes <= 0)
        {
            errors.Add("OperationalNotifications:EscalationWindowMinutes must be greater than zero.");
        }

        if (options.EscalationScanIntervalSeconds <= 0)
        {
            errors.Add("OperationalNotifications:EscalationScanIntervalSeconds must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

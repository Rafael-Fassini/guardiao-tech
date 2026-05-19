namespace Guardiao.Infrastructure.Security;

public sealed class SensitiveDataRedactor
{
    public string RedactIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[redacted]";
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 6)
        {
            return "[redacted]";
        }

        return $"{trimmed[..3]}***{trimmed[^3..]}";
    }

    public string RedactSecret(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "[redacted]" : "[redacted]";
    }
}

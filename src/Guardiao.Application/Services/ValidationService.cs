namespace Guardiao.Application.Services;

public static class ValidationService
{
    public static void ValidateString(string value, string fieldName, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required");
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            throw new ArgumentException($"{fieldName} exceeds maximum length of {maxLength.Value}");
        }
    }
}

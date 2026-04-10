namespace Guardiao.Application.Services;

public static class ValidationService
{
    public static void ValidateString(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{fieldName} is required");
    }
}

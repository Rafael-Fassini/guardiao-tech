using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Options;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = string.Empty;
    public int DefaultTtlSeconds { get; set; } = 300;
    public bool Enabled { get; set; } = false;
}

public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        var errors = new List<string>();

        if (options.DefaultTtlSeconds <= 0)
        {
            errors.Add("Redis:DefaultTtlSeconds must be greater than zero.");
        }

        if (options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            errors.Add("Redis:ConnectionString is required when Redis is enabled.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

public class ObjectStorageOptions
{
    public const string SectionName = "ObjectStorage";

    public string BucketName { get; set; } = "guardiao-evidence";
    public string RootPath { get; set; } = "/tmp/guardiao-object-storage";
    public bool Enabled { get; set; } = true;
    public long MaxObjectSizeBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp", "video/mp4"];
    public string[] AllowedFileExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".mp4"];
}

public sealed class ObjectStorageOptionsValidator : IValidateOptions<ObjectStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, ObjectStorageOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            errors.Add("ObjectStorage:BucketName is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            errors.Add("ObjectStorage:RootPath is required.");
        }

        if (options.MaxObjectSizeBytes <= 0)
        {
            errors.Add("ObjectStorage:MaxObjectSizeBytes must be greater than zero.");
        }

        if (options.AllowedContentTypes.Length == 0)
        {
            errors.Add("ObjectStorage:AllowedContentTypes must declare at least one value.");
        }

        if (options.AllowedFileExtensions.Length == 0)
        {
            errors.Add("ObjectStorage:AllowedFileExtensions must declare at least one value.");
        }

        if (options.AllowedFileExtensions.Any(x => string.IsNullOrWhiteSpace(x) || !x.StartsWith(".", StringComparison.Ordinal)))
        {
            errors.Add("ObjectStorage:AllowedFileExtensions must use dotted extensions.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

public class RetentionOptions
{
    public const string SectionName = "Retention";

    public int ShortLivedDays { get; set; } = 1;
    public int CaseBoundDays { get; set; } = 30;
    public int AuditOnlyDays { get; set; } = 365;
}

public sealed class RetentionOptionsValidator : IValidateOptions<RetentionOptions>
{
    public ValidateOptionsResult Validate(string? name, RetentionOptions options)
    {
        var errors = new List<string>();

        if (options.ShortLivedDays <= 0)
        {
            errors.Add("Retention:ShortLivedDays must be greater than zero.");
        }

        if (options.CaseBoundDays <= 0)
        {
            errors.Add("Retention:CaseBoundDays must be greater than zero.");
        }

        if (options.AuditOnlyDays <= 0)
        {
            errors.Add("Retention:AuditOnlyDays must be greater than zero.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

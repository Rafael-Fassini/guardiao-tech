using Guardiao.Infrastructure.Options;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

public class PersistenceOptionsValidatorTests
{
    [Fact]
    public void RedisValidator_ShouldFail_WhenEnabledWithoutConnectionString()
    {
        var validator = new RedisOptionsValidator();
        var result = validator.Validate(null, new RedisOptions
        {
            Enabled = true,
            ConnectionString = "",
            DefaultTtlSeconds = 10
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ObjectStorageValidator_ShouldFail_WhenRootPathIsMissing()
    {
        var validator = new ObjectStorageOptionsValidator();
        var result = validator.Validate(null, new ObjectStorageOptions
        {
            BucketName = "bucket",
            RootPath = "",
            AllowedContentTypes = ["image/jpeg"],
            AllowedFileExtensions = [".jpg"]
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ObjectStorageValidator_ShouldFail_WhenNoContentTypesAreConfigured()
    {
        var validator = new ObjectStorageOptionsValidator();
        var result = validator.Validate(null, new ObjectStorageOptions
        {
            BucketName = "bucket",
            RootPath = "/tmp/storage",
            AllowedContentTypes = [],
            AllowedFileExtensions = [".jpg"]
        });

        Assert.False(result.Succeeded);
    }
}

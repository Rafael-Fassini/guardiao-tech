using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace Guardiao.UnitTests.Infrastructure;

[Trait("Category", "Security")]
public class EvidenceStorageSecurityTests
{
    [Fact]
    public async Task StoreAsync_ShouldRejectDisallowedContentType()
    {
        var adapter = CreateAdapter();
        await using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.StoreAsync(content, "evidence.jpg", "text/plain"));
    }

    [Fact]
    public async Task StoreAsync_ShouldRejectOversizedObject()
    {
        var adapter = CreateAdapter(maxObjectSizeBytes: 4);
        await using var content = new MemoryStream([1, 2, 3, 4, 5]);

        await Assert.ThrowsAsync<ArgumentException>(() => adapter.StoreAsync(content, "evidence.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task StoreAsync_ShouldGenerateInternalObjectName()
    {
        var root = Path.Combine(Path.GetTempPath(), "guardiao-storage-tests", Guid.NewGuid().ToString("N"));
        var adapter = CreateAdapter(root);
        await using var content = new MemoryStream([1, 2, 3]);

        var objectKey = await adapter.StoreAsync(content, "sensitive-name.jpg", "image/jpeg");

        Assert.DoesNotContain("sensitive-name", objectKey, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".jpg", objectKey, StringComparison.OrdinalIgnoreCase);
    }

    private static MinioEvidenceStorageAdapter CreateAdapter(string? root = null, long maxObjectSizeBytes = 1024)
    {
        return new MinioEvidenceStorageAdapter(Options.Create(new ObjectStorageOptions
        {
            BucketName = "test-bucket",
            RootPath = root ?? Path.Combine(Path.GetTempPath(), "guardiao-storage-tests", Guid.NewGuid().ToString("N")),
            MaxObjectSizeBytes = maxObjectSizeBytes,
            AllowedContentTypes = ["image/jpeg"],
            AllowedFileExtensions = [".jpg"]
        }));
    }
}

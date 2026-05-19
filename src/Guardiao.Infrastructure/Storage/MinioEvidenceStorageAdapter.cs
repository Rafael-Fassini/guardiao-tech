using Guardiao.Application.Ports.Outbound;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Storage;

public sealed class MinioEvidenceStorageAdapter : IEvidenceStoragePort
{
    private readonly ObjectStorageOptions _options;

    public MinioEvidenceStorageAdapter(IOptions<ObjectStorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<string> StoreAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        Validate(fileName, contentType);
        Directory.CreateDirectory(_options.RootPath);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var objectKey = $"{_options.BucketName}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_options.RootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = File.Create(fullPath);
        await CopyWithLimitAsync(content, output, _options.MaxObjectSizeBytes, cancellationToken);
        return objectKey;
    }

    private void Validate(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !_options.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("File extension is not allowed.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(contentType) || !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Content type is not allowed.", nameof(contentType));
        }
    }

    private static async Task CopyWithLimitAsync(Stream input, Stream output, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long totalBytes = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                throw new ArgumentException("Object size exceeded the configured limit.", nameof(input));
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }
}

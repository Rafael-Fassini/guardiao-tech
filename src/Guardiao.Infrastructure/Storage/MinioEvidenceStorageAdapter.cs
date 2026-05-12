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

    public async Task<string> StoreAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.RootPath);

        var objectKey = $"{_options.BucketName}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}-{Sanitize(fileName)}";
        var fullPath = Path.Combine(_options.RootPath, objectKey.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var output = File.Create(fullPath);
        await content.CopyToAsync(output, cancellationToken);
        return objectKey;
    }

    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(fileName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

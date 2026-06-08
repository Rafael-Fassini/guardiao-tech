using Guardiao.Infrastructure.Options;
using Guardiao.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Guardiao.Api.Infrastructure;

public sealed class ApiReadinessService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ObjectStorageOptions _storageOptions;

    public ApiReadinessService(IServiceScopeFactory scopeFactory, IOptions<ObjectStorageOptions> storageOptions)
    {
        _scopeFactory = scopeFactory;
        _storageOptions = storageOptions.Value;
    }

    public async Task<ApiReadinessStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        bool databaseReachable;
        bool migrationsApplied;
        string? databaseError = null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GuardiaoDbContext>();
            databaseReachable = await dbContext.Database.CanConnectAsync(cancellationToken);
            migrationsApplied = databaseReachable && !(await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
        }
        catch (Exception ex)
        {
            databaseReachable = false;
            migrationsApplied = false;
            databaseError = ex.Message;
        }

        var storageCheck = CheckObjectStorage();

        return new ApiReadinessStatus(
            databaseReachable && migrationsApplied && storageCheck.IsWritable,
            databaseReachable,
            migrationsApplied,
            storageCheck.IsWritable,
            databaseError,
            storageCheck.Error);
    }

    private StorageCheckResult CheckObjectStorage()
    {
        try
        {
            Directory.CreateDirectory(_storageOptions.RootPath);
            var markerPath = Path.Combine(_storageOptions.RootPath, $".ready-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(markerPath, "ready");
            File.Delete(markerPath);
            return new StorageCheckResult(true, null);
        }
        catch (Exception ex)
        {
            return new StorageCheckResult(false, ex.Message);
        }
    }

    public sealed record ApiReadinessStatus(
        bool IsReady,
        bool DatabaseReachable,
        bool MigrationsApplied,
        bool ObjectStorageWritable,
        string? DatabaseError,
        string? ObjectStorageError);

    private sealed record StorageCheckResult(bool IsWritable, string? Error);
}

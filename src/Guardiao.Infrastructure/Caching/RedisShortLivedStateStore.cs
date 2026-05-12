using System.Collections.Concurrent;
using Guardiao.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Guardiao.Infrastructure.Caching;

public interface IShortLivedStateStore
{
    Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

internal sealed class CacheEntry
{
    public required string Value { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
}

public sealed class RedisShortLivedStateStore : IShortLivedStateStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly RedisOptions _options;

    public RedisShortLivedStateStore(IOptions<RedisOptions> options)
    {
        _options = options.Value;
    }

    public Task SetAsync(string key, string value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var effectiveTtl = ttl ?? TimeSpan.FromSeconds(_options.DefaultTtlSeconds);
        _entries[key] = new CacheEntry
        {
            Value = value,
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(effectiveTtl)
        };

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return Task.FromResult<string?>(null);
        }

        if (entry.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry.Value);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

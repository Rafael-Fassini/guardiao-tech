using System.Collections.Concurrent;
using Guardiao.Application.Ports.Outbound;

namespace Guardiao.Api.Infrastructure;

public sealed class ApiMetricsCollector : IMetricsPort
{
    private readonly ConcurrentDictionary<string, double> _counters = new();
    private readonly ConcurrentDictionary<string, double> _gauges = new();

    public void IncrementCounter(string name, params (string Key, string Value)[] tags)
    {
        _counters.AddOrUpdate(FormatKey(name, tags), 1, (_, current) => current + 1);
    }

    public void RecordLatency(string name, TimeSpan elapsed, params (string Key, string Value)[] tags)
    {
        _gauges[FormatKey(name, tags)] = elapsed.TotalMilliseconds;
    }

    public void RecordGauge(string name, double value, params (string Key, string Value)[] tags)
    {
        _gauges[FormatKey(name, tags)] = value;
    }

    public IReadOnlyDictionary<string, double> SnapshotCounters() => _counters;
    public IReadOnlyDictionary<string, double> SnapshotGauges() => _gauges;

    private static string FormatKey(string name, params (string Key, string Value)[] tags)
    {
        return tags.Length == 0
            ? name
            : $"{name}|{string.Join(",", tags.Select(x => $"{x.Key}={x.Value}"))}";
    }
}

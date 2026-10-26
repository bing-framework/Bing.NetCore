using System.Diagnostics.Metrics;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 监听 Bing 混合缓存回源准入诊断的测试收集器。
/// </summary>
/// <remarks>
/// 按缓存实例名称过滤指标，并记录是否出现了可能泄露键或标签的维度，供并行测试独立断言。
/// </remarks>
internal sealed class FactoryMetrics : IDisposable
{
    /// <summary>
    /// 接收指定 Meter 测量值的监听器。
    /// </summary>
    private readonly MeterListener _listener = new();

    /// <summary>
    /// 不允许出现在指标维度中的敏感键或标签值。
    /// </summary>
    private readonly string[] _sensitiveValues;

    /// <summary>
    /// 当前等待数、执行数、拒绝数及各自的峰值。
    /// </summary>
    internal long Waiting, Active, Rejected, PeakWaiting, PeakActive;

    /// <summary>
    /// 等待耗时样本数与疑似泄露敏感维度的测量次数。
    /// </summary>
    internal long WaitDurationCount, UnsafeDimensionCount;

    /// <summary>
    /// 累计等待耗时，单位为毫秒。
    /// </summary>
    internal double WaitDurationMilliseconds;

    /// <summary>
    /// 初始化一个 <see cref="FactoryMetrics" /> 类的新实例。
    /// </summary>
    /// <param name="name">只统计该缓存实例名称的指标。</param>
    /// <param name="sensitiveValues">不允许出现在维度值中的敏感文本。</param>
    internal FactoryMetrics(string name, params string[] sensitiveValues)
    {
        _sensitiveValues = sensitiveValues;
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Bing.Caching.FusionCache")
                listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, delta, tags, _) =>
        {
            var matched = false;
            foreach (var tag in tags)
            {
                InspectDimension(tag, ref UnsafeDimensionCount);
                if (tag.Key == "cache.name" && Equals(tag.Value, name)) matched = true;
            }
            if (!matched) return;
            if (instrument.Name.EndsWith(".waiting"))
            {
                var value = Interlocked.Add(ref Waiting, delta);
                UpdatePeak(ref PeakWaiting, value);
            }
            if (instrument.Name.EndsWith(".active"))
            {
                var value = Interlocked.Add(ref Active, delta);
                UpdatePeak(ref PeakActive, value);
            }
            if (instrument.Name.EndsWith(".rejected")) Interlocked.Add(ref Rejected, delta);
        });
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            var matched = false;
            foreach (var tag in tags)
            {
                InspectDimension(tag, ref UnsafeDimensionCount);
                if (tag.Key == "cache.name" && Equals(tag.Value, name)) matched = true;
            }
            if (matched && instrument.Name.EndsWith(".wait.duration"))
            {
                Interlocked.Increment(ref WaitDurationCount);
                Add(ref WaitDurationMilliseconds, value);
            }
        });
        _listener.Start();
    }

    /// <summary>
    /// 检查单个指标维度是否包含键、标签或敏感值。
    /// </summary>
    /// <param name="tag">待检查的维度键值对。</param>
    /// <param name="unsafeCount">发现敏感维度时递增的计数。</param>
    private void InspectDimension(KeyValuePair<string, object?> tag, ref long unsafeCount)
    {
        var key = tag.Key;
        var value = Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (key.Contains("key", StringComparison.OrdinalIgnoreCase)
            || key.Contains("tag", StringComparison.OrdinalIgnoreCase)
            || _sensitiveValues.Any(sensitive => value.Contains(sensitive, StringComparison.Ordinal)))
            Interlocked.Increment(ref unsafeCount);
    }

    /// <summary>
    /// 以原子方式把值累加到双精度目标。
    /// </summary>
    /// <param name="target">累加目标。</param>
    /// <param name="value">本次增量。</param>
    private static void Add(ref double target, double value)
    {
        double current;
        do { current = Volatile.Read(ref target); }
        while (Interlocked.CompareExchange(ref target, current + value, current) != current);
    }

    /// <summary>
    /// 以原子方式更新长整型峰值。
    /// </summary>
    /// <param name="peak">峰值目标。</param>
    /// <param name="value">本次观测值。</param>
    private static void UpdatePeak(ref long peak, long value)
    {
        long current;
        do { current = Volatile.Read(ref peak); if (current >= value) return; }
        while (Interlocked.CompareExchange(ref peak, value, current) != current);
    }

    /// <summary>
    /// 停止监听并释放 Meter 订阅。
    /// </summary>
    public void Dispose() => _listener.Dispose();
}

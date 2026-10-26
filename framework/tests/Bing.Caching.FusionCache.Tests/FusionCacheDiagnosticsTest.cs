using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bing.Caching.FusionCache.Tests;

/// <summary>
/// 验证混合缓存事件、指标和 Activity 反映真实缓存行为且不泄露敏感维度。
/// </summary>
public class FusionCacheDiagnosticsTest
{
    /// <summary>
    /// 验证命中、未命中、Fail-Safe、回源错误和 Activity 均被上报，且 Bing 维度不包含键或标签。
    /// </summary>
    [Fact]
    public async Task EventsMetricsAndActivities_ReportRealCacheBehaviorAndBoundSensitiveDimensions()
    {
        var name = Guid.NewGuid().ToString("N");
        const string sensitiveKey = "customer-secret-8142";
        const string sensitiveTag = "account-secret-6219";
        var nativeKey = "d:" + Uri.EscapeDataString(sensitiveKey);
        using var admission = new FactoryMetrics(name, sensitiveKey, sensitiveTag);
        using var nativeMetrics = new NativeMetricObserver(name, sensitiveKey, sensitiveTag);
        using var activities = new NativeActivityObserver();
        using var services = TestCache.Create(o => { }, b => b.WithDefaultEntryOptions(options =>
        {
            options.IsFailSafeEnabled = true;
            options.FailSafeMaxDuration = TimeSpan.FromMinutes(1);
        }), name);
        var cache = services.GetRequiredService<IHybridCache>();
        var fusionCache = services.GetRequiredService<IFusionCacheProvider>().GetCache(name);
        var hits = 0;
        var misses = 0;
        var failSafeActivations = 0;
        var failSafeCallbacks = 0;
        var factoryErrors = 0;
        var hitCallbacks = 0;

        fusionCache.Events.Hit += (_, args) =>
        {
            Interlocked.Increment(ref hitCallbacks);
            if (IsBusinessEntry(args.Key)) Interlocked.Increment(ref hits);
        };
        fusionCache.Events.Miss += (_, args) =>
        {
            if (IsBusinessEntry(args.Key)) Interlocked.Increment(ref misses);
        };
        fusionCache.Events.FailSafeActivate += (_, args) =>
        {
            Interlocked.Increment(ref failSafeCallbacks);
            if (IsBusinessEntry(args.Key)) Interlocked.Increment(ref failSafeActivations);
        };
        fusionCache.Events.FactoryError += (_, args) =>
        {
            if (IsBusinessEntry(args.Key)) Interlocked.Increment(ref factoryErrors);
        };

        Assert.True(fusionCache.DefaultEntryOptions.IsFailSafeEnabled);

        await cache.SetAsync(sensitiveKey, 42, tags: [sensitiveTag]);
        Assert.Equal(42, (await cache.TryGetAsync<int>(sensitiveKey)).Value);
        Assert.Equal(7, await cache.GetOrCreateAsync("factory-observation", _ => Task.FromResult(7), tags: [sensitiveTag]));
        await cache.ExpireAsync(sensitiveKey);
        Assert.False((await cache.TryGetAsync<int>(sensitiveKey)).Hit);
        var failedFactoryCalls = 0;
        Assert.Equal(42, await cache.GetOrCreateAsync<int>(sensitiveKey,
            _ =>
            {
                Interlocked.Increment(ref failedFactoryCalls);
                return Task.FromException<int>(new InvalidOperationException("injected"));
            }, tags: [sensitiveTag]));
        Assert.Equal(1, failedFactoryCalls);

        await TestCache.Eventually(() => Task.FromResult(
            Volatile.Read(ref hits) > 0
            && Volatile.Read(ref misses) > 0
            && Volatile.Read(ref failSafeActivations) > 0
            && Volatile.Read(ref factoryErrors) > 0));

        Assert.True(Volatile.Read(ref hits) > 0, $"Hit callbacks={Volatile.Read(ref hitCallbacks)}; matching data hits={Volatile.Read(ref hits)}.");
        Assert.True(Volatile.Read(ref misses) > 0);
        Assert.True(Volatile.Read(ref failSafeActivations) > 0,
            $"Fail-safe callbacks={Volatile.Read(ref failSafeCallbacks)}; matching data activations={Volatile.Read(ref failSafeActivations)}; factory errors={Volatile.Read(ref factoryErrors)}.");
        Assert.True(admission.WaitDurationCount > 0);
        Assert.True(admission.WaitDurationMilliseconds >= 0);
        Assert.Equal(0, admission.UnsafeDimensionCount);
        Assert.True(nativeMetrics.MeasurementCount > 0);
        Assert.True(nativeMetrics.Count("fusioncache.cache.hit") > 0);
        Assert.True(nativeMetrics.Count("fusioncache.cache.miss") > 0);
        Assert.Equal(1, nativeMetrics.Count("fusioncache.failsafe_activate"));
        Assert.Equal(0, nativeMetrics.UnsafeDimensionCount);

        var traces = activities.Snapshots;
        Assert.NotEmpty(traces);
        var nativeTraces = traces.Where(trace => trace.SourceName == FusionCacheDiagnostics.ActivitySourceName).ToArray();
        Assert.NotEmpty(nativeTraces);
        Assert.Contains(nativeTraces, trace => trace.Tags.Any(tag => tag.Value?.ToString()?.Contains(nativeKey, StringComparison.Ordinal) == true));

        var bingTraces = traces.Where(trace => trace.SourceName == "Bing.Caching.FusionCache").ToArray();
        Assert.NotEmpty(bingTraces);
        Assert.All(bingTraces, trace => Assert.DoesNotContain(trace.Tags, tag =>
        {
            var key = tag.Key;
            var value = Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            return key.Contains("key", StringComparison.OrdinalIgnoreCase)
                || key.Contains("tag", StringComparison.OrdinalIgnoreCase)
                || value.Contains(sensitiveKey, StringComparison.Ordinal)
                || value.Contains(sensitiveTag, StringComparison.Ordinal);
        }));
    }

    /// <summary>
    /// 判断原生缓存键是否属于业务数据条目。
    /// </summary>
    /// <param name="key">原生缓存键。</param>
    /// <returns>数据条目返回 <see langword="true" />；标签或清除元数据返回 <see langword="false" />。</returns>
    private static bool IsBusinessEntry(string key) => key.Contains(":d:", StringComparison.Ordinal);

    /// <summary>
    /// 收集 FusionCache 原生指标并检查维度安全性。
    /// </summary>
    private sealed class NativeMetricObserver : IDisposable
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
        /// 按指标名称累计的匹配测量值。
        /// </summary>
        private readonly ConcurrentDictionary<string, long> _counts = new();

        /// <summary>
        /// 接收到的测量总数。
        /// </summary>
        internal long MeasurementCount;

        /// <summary>
        /// 包含疑似敏感维度的测量次数。
        /// </summary>
        internal long UnsafeDimensionCount;

        /// <summary>
        /// 获取指定原生指标的累计值。
        /// </summary>
        /// <param name="instrument">原生指标名称。</param>
        /// <returns>累计值；未收到测量时返回零。</returns>
        internal long Count(string instrument) => _counts.GetValueOrDefault(instrument);

        /// <summary>
        /// 初始化一个 <see cref="NativeMetricObserver" /> 类的新实例。
        /// </summary>
        /// <param name="cacheName">只统计该原生缓存名称的指标。</param>
        /// <param name="sensitiveValues">不允许出现在维度值中的敏感文本。</param>
        internal NativeMetricObserver(string cacheName, params string[] sensitiveValues)
        {
            _sensitiveValues = sensitiveValues;
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name.StartsWith(FusionCacheDiagnostics.MeterName, StringComparison.Ordinal))
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                Record(tags);
                foreach (var tag in tags)
                    if (tag.Key == "fusioncache.cache.name" && Equals(tag.Value, cacheName))
                        _counts.AddOrUpdate(instrument.Name, value, (_, previous) => previous + value);
            });
            _listener.SetMeasurementEventCallback<double>((_, _, tags, _) => Record(tags));
            _listener.Start();
        }

        /// <summary>
        /// 记录一次测量并检查其维度是否包含敏感内容。
        /// </summary>
        /// <param name="tags">本次测量的维度集合。</param>
        private void Record(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            Interlocked.Increment(ref MeasurementCount);
            foreach (var tag in tags)
            {
                var key = tag.Key;
                var value = Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                if (key.Contains("key", StringComparison.OrdinalIgnoreCase)
                    || key.Contains("tag", StringComparison.OrdinalIgnoreCase)
                    || value.Contains(":d:", StringComparison.Ordinal)
                    || value.Contains(":t:", StringComparison.Ordinal)
                    || _sensitiveValues.Any(sensitive => value.Contains(sensitive, StringComparison.Ordinal)))
                    Interlocked.Increment(ref UnsafeDimensionCount);
            }
        }

        /// <summary>
        /// 停止监听并释放 Meter 订阅。
        /// </summary>
        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// 收集 FusionCache 原生与 Bing Activity 的快照。
    /// </summary>
    private sealed class NativeActivityObserver : IDisposable
    {
        /// <summary>
        /// 已停止 Activity 的线程安全快照队列。
        /// </summary>
        private readonly ConcurrentQueue<ActivitySnapshot> _snapshots = new();

        /// <summary>
        /// 只监听 FusionCache 与 Bing 缓存 ActivitySource 的监听器。
        /// </summary>
        private readonly ActivityListener _listener = new();

        /// <summary>
        /// 获取已收集的 Activity 快照。
        /// </summary>
        internal IReadOnlyCollection<ActivitySnapshot> Snapshots => _snapshots.ToArray();

        /// <summary>
        /// 初始化一个 <see cref="NativeActivityObserver" /> 类的新实例。
        /// </summary>
        internal NativeActivityObserver()
        {
            _listener.ShouldListenTo = source => source.Name == FusionCacheDiagnostics.ActivitySourceName
                || source.Name == "Bing.Caching.FusionCache";
            _listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
            _listener.ActivityStopped = activity =>
                _snapshots.Enqueue(new ActivitySnapshot(activity.Source.Name, activity.TagObjects.ToArray()));
            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>
        /// 停止监听并移除 Activity 监听器。
        /// </summary>
        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// 保存 Activity 来源名称与标签的不可变快照。
    /// </summary>
    /// <param name="SourceName">产生 Activity 的源名称。</param>
    /// <param name="Tags">Activity 停止时捕获的标签。</param>
    private sealed record ActivitySnapshot(string SourceName, KeyValuePair<string, object?>[] Tags);
}

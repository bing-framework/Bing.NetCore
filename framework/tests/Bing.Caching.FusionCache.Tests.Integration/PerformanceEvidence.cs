using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Events;

namespace Bing.Caching.FusionCache.Tests.Integration;

/// <summary>
/// 保存一次缓存资源测量结果的不可变证据记录。
/// </summary>
/// <remarks>
/// 仅用于小规模资源观察，不设置性能提升或生产容量门槛。
/// </remarks>
/// <param name="Scenario">测量场景名称。</param>
/// <param name="Runtime">运行时框架描述。</param>
/// <param name="Samples">延迟样本数量。</param>
/// <param name="ElapsedMs">总耗时，单位为毫秒。</param>
/// <param name="MeanUs">平均延迟，单位为微秒。</param>
/// <param name="P50Us">P50 延迟，单位为微秒；样本不足时为 <see langword="null" />。</param>
/// <param name="P95Us">P95 延迟，单位为微秒；样本不足时为 <see langword="null" />。</param>
/// <param name="P99Us">P99 延迟，单位为微秒；样本不足时为 <see langword="null" />。</param>
/// <param name="AllocatedBytes">测量期间分配的总字节数。</param>
/// <param name="FactoryCalls">回源工厂调用次数。</param>
/// <param name="HitCount">原生缓存命中次数；未观测时为 <see langword="null" />。</param>
/// <param name="MissCount">原生缓存未命中次数；未观测时为 <see langword="null" />。</param>
/// <param name="FailSafeCount">Fail-Safe 激活次数；未观测时为 <see langword="null" />。</param>
/// <param name="ProcessCpuMs">测量期间进程 CPU 时间，单位为毫秒。</param>
/// <param name="WorkingSetStartBytes">测量开始时的工作集字节数。</param>
/// <param name="WorkingSetEndBytes">测量结束时的工作集字节数。</param>
/// <param name="WorkingSetPeakBytes">测量期间的工作集峰值字节数。</param>
/// <param name="WorkingSetSampleIntervalMs">工作集采样间隔，单位为毫秒。</param>
/// <param name="PeakActive">回源工厂并发峰值。</param>
/// <param name="PeakWaiting">等待回源许可的峰值数量。</param>
internal sealed record PerformanceEvidence(string Scenario, string Runtime, int Samples, double ElapsedMs,
    double MeanUs, double? P50Us, double? P95Us, double? P99Us, long AllocatedBytes, int FactoryCalls,
    int? HitCount, int? MissCount, int? FailSafeCount, double? ProcessCpuMs,
    long? WorkingSetStartBytes, long? WorkingSetEndBytes, long? WorkingSetPeakBytes,
    int? WorkingSetSampleIntervalMs, long PeakActive, long PeakWaiting)
{
    /// <summary>
    /// 创建一次缓存资源测量的性能证据。
    /// </summary>
    /// <param name="scenario">测量场景名称。</param>
    /// <param name="latencyUs">各样本延迟，单位为微秒。</param>
    /// <param name="started">测量开始时的 <see cref="Stopwatch" /> 时间戳。</param>
    /// <param name="allocationStart">测量开始前的累计分配字节数。</param>
    /// <param name="factories">回源工厂调用次数。</param>
    /// <param name="observer">回源准入观测器。</param>
    /// <returns>不包含原生缓存事件计数的性能证据。</returns>
    /// <remarks>
    /// 使用调用方提供的时间戳与分配基线，原生缓存事件计数置为 <see langword="null" />。
    /// </remarks>
    internal static PerformanceEvidence Create(string scenario, double[] latencyUs, long started, long allocationStart,
        int factories, AdmissionObserver observer)
    {
        return CreateCore(scenario, latencyUs, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            GC.GetTotalAllocatedBytes(precise: true) - allocationStart, factories, observer,
            null, null, null, null);
    }

    /// <summary>
    /// 创建一次缓存资源测量的性能证据。
    /// </summary>
    /// <param name="scenario">测量场景名称。</param>
    /// <param name="latencyUs">各样本延迟，单位为微秒。</param>
    /// <param name="factories">回源工厂调用次数。</param>
    /// <param name="observer">回源准入观测器。</param>
    /// <param name="snapshot">进程资源测量快照。</param>
    /// <param name="events">原生缓存事件观测器。</param>
    /// <returns>包含原生缓存命中、未命中和 Fail-Safe 计数的性能证据。</returns>
    /// <remarks>
    /// 使用完整资源快照，并附带原生缓存的命中、未命中和 Fail-Safe 计数。
    /// </remarks>
    internal static PerformanceEvidence Create(string scenario, double[] latencyUs, int factories,
        AdmissionObserver observer, PerformanceSnapshot snapshot, NativeCacheEventObserver events)
    {
        return CreateCore(scenario, latencyUs, snapshot.ElapsedMilliseconds, snapshot.AllocatedBytes,
            factories, observer, snapshot, events.HitCount, events.MissCount, events.FailSafeCount);
    }

    /// <summary>
    /// 排序延迟样本并组装最终证据记录。
    /// </summary>
    /// <param name="scenario">测量场景名称。</param>
    /// <param name="latencyUs">各样本延迟，单位为微秒。</param>
    /// <param name="elapsedMs">总耗时，单位为毫秒。</param>
    /// <param name="allocatedBytes">测量期间分配的总字节数。</param>
    /// <param name="factories">回源工厂调用次数。</param>
    /// <param name="observer">回源准入观测器。</param>
    /// <param name="snapshot">可选的进程资源测量快照。</param>
    /// <param name="hitCount">可选的命中次数。</param>
    /// <param name="missCount">可选的未命中次数。</param>
    /// <param name="failSafeCount">可选的 Fail-Safe 激活次数。</param>
    /// <returns>完成百分位计算的性能证据。</returns>
    private static PerformanceEvidence CreateCore(string scenario, double[] latencyUs, double elapsedMs,
        long allocatedBytes, int factories, AdmissionObserver observer, PerformanceSnapshot? snapshot,
        int? hitCount, int? missCount, int? failSafeCount)
    {
        Array.Sort(latencyUs);
        return new(scenario, RuntimeInformation.FrameworkDescription, latencyUs.Length,
            elapsedMs, latencyUs.Average(), Percentile(latencyUs, 0.50), Percentile(latencyUs, 0.95),
            Percentile(latencyUs, 0.99), allocatedBytes, factories, hitCount, missCount, failSafeCount,
            snapshot?.ProcessCpuMilliseconds, snapshot?.WorkingSetStartBytes, snapshot?.WorkingSetEndBytes,
            snapshot?.WorkingSetPeakBytes, snapshot?.SampleIntervalMilliseconds, observer.PeakActive, observer.PeakWaiting);
    }

    /// <summary>
    /// 按 floor((样本数 - 1) × percentile) 的索引从已排序样本中取样。
    /// </summary>
    /// <param name="sortedSamples">已升序排序的延迟样本。</param>
    /// <param name="percentile">取值区间为 0 到 1 的百分位。</param>
    /// <returns>百分位延迟；样本少于两个时返回 <see langword="null" />。</returns>
    private static double? Percentile(double[] sortedSamples, double percentile) =>
        sortedSamples.Length < 2 ? null : sortedSamples[(int)Math.Floor((sortedSamples.Length - 1) * percentile)];

    /// <summary>
    /// 把证据写入测试输出，并在配置结果目录时合并保存为 UTF-8 JSON 文件。
    /// </summary>
    /// <param name="output">测试输出写入器。</param>
    internal void Save(ITestOutputHelper output)
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        output.WriteLine(JsonSerializer.Serialize(this, jsonOptions));
        var root = Environment.GetEnvironmentVariable("BING_TEST_FUSIONCACHE_RESULTS");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"performance-net{Environment.Version.Major}.json");
        var records = File.Exists(path)
            ? JsonSerializer.Deserialize<List<PerformanceEvidence>>(File.ReadAllText(path, Encoding.UTF8))!
            : new List<PerformanceEvidence>();
        records.RemoveAll(record => record.Scenario == Scenario);
        records.Add(this);
        File.WriteAllText(path, JsonSerializer.Serialize(records, jsonOptions), new UTF8Encoding(false));
    }
}

/// <summary>
/// 采样当前进程工作集、CPU 时间和分配量的一次性资源测量器。
/// </summary>
internal sealed class PerformanceMeasurement : IDisposable
{
    /// <summary>
    /// 工作集采样的默认间隔，单位为毫秒。
    /// </summary>
    private const int DefaultSampleIntervalMilliseconds = 10;

    /// <summary>
    /// 当前进程句柄。
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// 周期刷新进程工作集的计时器。
    /// </summary>
    private readonly Timer _timer;

    /// <summary>
    /// 测量开始时的 <see cref="Stopwatch" /> 时间戳。
    /// </summary>
    private readonly long _started;

    /// <summary>
    /// 测量开始前的累计分配字节数。
    /// </summary>
    private readonly long _allocationStart;

    /// <summary>
    /// 测量开始前的进程 CPU 时间。
    /// </summary>
    private readonly TimeSpan _cpuStart;

    /// <summary>
    /// 测量开始时的工作集字节数。
    /// </summary>
    private readonly long _workingSetStart;

    /// <summary>
    /// 工作集采样间隔，单位为毫秒。
    /// </summary>
    private readonly int _sampleIntervalMilliseconds;

    /// <summary>
    /// 测量期间观察到的工作集峰值字节数。
    /// </summary>
    private long _workingSetPeak;

    /// <summary>
    /// 标记测量是否已经完成或被释放，防止重复结算。
    /// </summary>
    private int _completed;

    /// <summary>
    /// 初始化一个 <see cref="PerformanceMeasurement" /> 类型的实例。
    /// </summary>
    /// <param name="sampleIntervalMilliseconds">工作集采样间隔，单位为毫秒。</param>
    /// <remarks>
    /// 初始化一个 <see cref="PerformanceMeasurement" /> 类的新实例。
    /// </remarks>
    private PerformanceMeasurement(int sampleIntervalMilliseconds)
    {
        _sampleIntervalMilliseconds = sampleIntervalMilliseconds;
        _process = Process.GetCurrentProcess();
        _process.Refresh();
        _started = Stopwatch.GetTimestamp();
        _allocationStart = GC.GetTotalAllocatedBytes(precise: true);
        _cpuStart = _process.TotalProcessorTime;
        _workingSetStart = _process.WorkingSet64;
        _workingSetPeak = _workingSetStart;
        _timer = new Timer(SampleWorkingSet, null, sampleIntervalMilliseconds, sampleIntervalMilliseconds);
    }

    /// <summary>
    /// 启动资源测量并开始周期采样工作集。
    /// </summary>
    /// <param name="sampleIntervalMilliseconds">工作集采样间隔，单位为毫秒。</param>
    /// <returns>已启动的资源测量器。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sampleIntervalMilliseconds" /> 小于等于零时抛出。</exception>
    internal static PerformanceMeasurement Start(int sampleIntervalMilliseconds = DefaultSampleIntervalMilliseconds)
    {
        if (sampleIntervalMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(sampleIntervalMilliseconds));
        return new PerformanceMeasurement(sampleIntervalMilliseconds);
    }

    /// <summary>
    /// 停止采样并返回本次资源测量快照。
    /// </summary>
    /// <returns>包含耗时、分配、CPU 和工作集的测量快照。</returns>
    /// <exception cref="InvalidOperationException">当前测量器已完成或已释放时抛出。</exception>
    internal PerformanceSnapshot Complete()
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            throw new InvalidOperationException("性能测量只能完成一次。");

        using (var callbacksStopped = new ManualResetEvent(false))
        {
            if (_timer.Dispose(callbacksStopped)) callbacksStopped.WaitOne();
        }

        _process.Refresh();
        var workingSetEnd = _process.WorkingSet64;
        UpdatePeak(workingSetEnd);
        var elapsed = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - _allocationStart;
        var cpuMilliseconds = (_process.TotalProcessorTime - _cpuStart).TotalMilliseconds;
        return new PerformanceSnapshot(elapsed, allocated, cpuMilliseconds, _workingSetStart,
            workingSetEnd, Volatile.Read(ref _workingSetPeak), _sampleIntervalMilliseconds);
    }

    /// <summary>
    /// 计时器回调，刷新进程工作集并更新峰值。
    /// </summary>
    /// <param name="state">计时器状态，未使用。</param>
    private void SampleWorkingSet(object? state)
    {
        try
        {
            _process.Refresh();
            UpdatePeak(_process.WorkingSet64);
        }
        catch (InvalidOperationException) { }
    }

    /// <summary>
    /// 以原子方式更新工作集峰值。
    /// </summary>
    /// <param name="value">本次观测的工作集字节数。</param>
    private void UpdatePeak(long value)
    {
        long current;
        do { current = Volatile.Read(ref _workingSetPeak); if (current >= value) return; }
        while (Interlocked.CompareExchange(ref _workingSetPeak, value, current) != current);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>停止计时器并释放进程句柄。</para>
    /// </remarks>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _completed, 1) == 0) _timer.Dispose();
        _process.Dispose();
    }
}

/// <summary>
/// 保存一次性能测量的进程资源快照。
/// </summary>
/// <param name="ElapsedMilliseconds">测量总耗时，单位为毫秒。</param>
/// <param name="AllocatedBytes">测量期间分配的总字节数。</param>
/// <param name="ProcessCpuMilliseconds">测量期间进程 CPU 时间，单位为毫秒。</param>
/// <param name="WorkingSetStartBytes">测量开始时的工作集字节数。</param>
/// <param name="WorkingSetEndBytes">测量结束时的工作集字节数。</param>
/// <param name="WorkingSetPeakBytes">测量期间的工作集峰值字节数。</param>
/// <param name="SampleIntervalMilliseconds">工作集采样间隔，单位为毫秒。</param>
internal sealed record PerformanceSnapshot(double ElapsedMilliseconds, long AllocatedBytes,
    double ProcessCpuMilliseconds, long WorkingSetStartBytes, long WorkingSetEndBytes,
    long WorkingSetPeakBytes, int SampleIntervalMilliseconds);

/// <summary>
/// 订阅原生 FusionCache 事件并统计数据条目的命中、未命中和 Fail-Safe 次数。
/// </summary>
internal sealed class NativeCacheEventObserver : IDisposable
{
    /// <summary>
    /// 原生 FusionCache 的事件中心。
    /// </summary>
    private readonly FusionCacheEventsHub _events;

    /// <summary>
    /// 数据条目命中次数。
    /// </summary>
    private int _hits;

    /// <summary>
    /// 数据条目未命中次数。
    /// </summary>
    private int _misses;

    /// <summary>
    /// 数据条目 Fail-Safe 激活次数。
    /// </summary>
    private int _failSafeCount;

    /// <summary>
    /// 初始化一个 <see cref="NativeCacheEventObserver" /> 类型的实例。
    /// </summary>
    /// <param name="cache">要观测的原生缓存实例。</param>
    ///
    /// <remarks>
    /// 构造时登记命中、未命中和 Fail-Safe 激活事件处理器。
    /// <para>
    /// 初始化一个 <see cref="NativeCacheEventObserver" /> 类的新实例。
    /// </para></remarks>
    internal NativeCacheEventObserver(IFusionCache cache)
    {
        _events = cache.Events;
        _events.Hit += OnHit;
        _events.Miss += OnMiss;
        _events.FailSafeActivate += OnFailSafeActivate;
    }

    /// <summary>
    /// 获取已观测到的数据条目命中次数。
    /// </summary>
    internal int HitCount => Volatile.Read(ref _hits);

    /// <summary>
    /// 获取已观测到的数据条目未命中次数。
    /// </summary>
    internal int MissCount => Volatile.Read(ref _misses);

    /// <summary>
    /// 获取已观测到的数据条目 Fail-Safe 激活次数。
    /// </summary>
    internal int FailSafeCount => Volatile.Read(ref _failSafeCount);

    /// <summary>
    /// 记录数据条目命中事件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="args">命中事件参数。</param>
    private void OnHit(object? sender, FusionCacheEntryHitEventArgs args)
    {
        if (IsDataKey(args.Key)) Interlocked.Increment(ref _hits);
    }

    /// <summary>
    /// 记录数据条目未命中事件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="args">未命中事件参数。</param>
    private void OnMiss(object? sender, FusionCacheEntryEventArgs args)
    {
        if (IsDataKey(args.Key)) Interlocked.Increment(ref _misses);
    }

    /// <summary>
    /// 记录数据条目 Fail-Safe 激活事件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="args">Fail-Safe 事件参数。</param>
    private void OnFailSafeActivate(object? sender, FusionCacheEntryEventArgs args)
    {
        if (IsDataKey(args.Key)) Interlocked.Increment(ref _failSafeCount);
    }

    /// <summary>
    /// 判断原生缓存键是否属于业务数据条目。
    /// </summary>
    /// <param name="key">原生缓存键。</param>
    /// <returns>数据条目返回 <see langword="true" />；元数据条目返回 <see langword="false" />。</returns>
    private static bool IsDataKey(string key) => key.Contains(":d:", StringComparison.Ordinal);

    /// <inheritdoc />
    /// <remarks>
    /// <para>取消事件订阅并释放观测器。</para>
    /// </remarks>
    public void Dispose()
    {
        _events.Hit -= OnHit;
        _events.Miss -= OnMiss;
        _events.FailSafeActivate -= OnFailSafeActivate;
    }
}

/// <summary>
/// 订阅 Bing 回源准入指标并记录并发与排队峰值。
/// </summary>
internal sealed class AdmissionObserver : IDisposable
{
    /// <summary>
    /// 接收 Bing 诊断 Meter 测量值的监听器。
    /// </summary>
    private readonly MeterListener _listener = new();

    /// <summary>
    /// 当前执行中与等待回源许可的工厂数量。
    /// </summary>
    private long _active, _waiting;

    /// <summary>
    /// 观测到的回源并发与排队峰值。
    /// </summary>
    internal long PeakActive, PeakWaiting;

    /// <summary>
    /// 初始化一个 <see cref="AdmissionObserver" /> 类型的实例。
    /// </summary>
    /// <param name="name">只统计该缓存实例名称的指标。</param>
    ///
    /// <remarks>
    /// 构造时开始监听 Bing 回源准入指标。
    /// <para>
    /// 初始化一个 <see cref="AdmissionObserver" /> 类的新实例。
    /// </para></remarks>
    internal AdmissionObserver(string name)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "Bing.Caching.FusionCache") listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, delta, tags, _) =>
        {
            var matches = false;
            foreach (var tag in tags) if (tag.Key == "cache.name" && Equals(tag.Value, name)) matches = true;
            if (!matches) return;
            if (instrument.Name.EndsWith(".active")) Peak(ref PeakActive, Interlocked.Add(ref _active, delta));
            if (instrument.Name.EndsWith(".waiting")) Peak(ref PeakWaiting, Interlocked.Add(ref _waiting, delta));
        });
        _listener.Start();
    }
    /// <summary>
    /// 以原子方式更新指定峰值。
    /// </summary>
    /// <param name="peak">峰值目标。</param>
    /// <param name="value">本次观测值。</param>
    private static void Peak(ref long peak, long value)
    {
        long old;
        do { old = Volatile.Read(ref peak); if (old >= value) return; }
        while (Interlocked.CompareExchange(ref peak, value, old) != old);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>停止监听并释放 Meter 订阅。</para>
    /// </remarks>
    public void Dispose() => _listener.Dispose();
}

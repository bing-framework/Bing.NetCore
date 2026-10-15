using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;

namespace Bing.Events.Ordering;

/// <summary>
/// 提供单个有序逻辑消费者的运行指标快照。
/// </summary>
public interface IOrderedEventMetrics
{
    /// <summary>
    /// 获取逻辑消费者名称。
    /// </summary>
    string ConsumerName { get; }

    /// <summary>
    /// 获取当前指标快照。
    /// </summary>
    BingOrderedEventMetricsSnapshot Snapshot { get; }
}

/// <summary>
/// 有序消费者的聚合指标，不包含事件 ID 或实体键等高基数标签。
/// </summary>
public sealed class BingOrderedEventMetricsSnapshot
{
    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsSnapshot" /> 类型的实例。
    /// </summary>
    /// <param name="consumerName">逻辑消费者名称。</param>
    /// <param name="pendingStreams">最近一次扫描发现的待处理流数。</param>
    /// <param name="processedCount">成功处理的事件数。</param>
    /// <param name="gapCount">发现前序缺口的次数。</param>
    /// <param name="failureCount">业务处理失败的次数。</param>
    /// <param name="retryCount">恢复事务已确认提交的重试次数。</param>
    /// <param name="inFlightCount">当前正在处理的事件数。</param>
    /// <param name="lastProcessingDurationMilliseconds">最近一次处理耗时，单位为毫秒。</param>
    public BingOrderedEventMetricsSnapshot(string consumerName, long pendingStreams, long processedCount,
        long gapCount, long failureCount, long retryCount, long inFlightCount,
        long lastProcessingDurationMilliseconds)
        : this(consumerName, pendingStreams, pendingStreams, processedCount, gapCount, failureCount,
            retryCount, inFlightCount, lastProcessingDurationMilliseconds, null, 0)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsSnapshot" /> 类型的实例。
    /// </summary>
    /// <param name="consumerName">逻辑消费者名称。</param>
    /// <param name="pendingStreams">最近一次扫描发现的待处理流数。</param>
    /// <param name="pendingEvents">最近一次统计发现的待处理事件数。</param>
    /// <param name="processedCount">成功处理的事件数。</param>
    /// <param name="gapCount">发现前序缺口的次数。</param>
    /// <param name="failureCount">业务处理失败的次数。</param>
    /// <param name="retryCount">恢复事务已确认提交的重试次数。</param>
    /// <param name="inFlightCount">当前正在处理的事件数。</param>
    /// <param name="lastProcessingDurationMilliseconds">最近一次处理耗时，单位为毫秒。</param>
    /// <param name="oldestPendingEventAge">最老待处理事件年龄；没有统计数据时返回 <see langword="null"/>。</param>
    public BingOrderedEventMetricsSnapshot(string consumerName, long pendingStreams, long pendingEvents,
        long processedCount, long gapCount, long failureCount, long retryCount, long inFlightCount,
        long lastProcessingDurationMilliseconds, TimeSpan? oldestPendingEventAge)
        : this(consumerName, pendingStreams, pendingEvents, processedCount, gapCount, failureCount,
            retryCount, inFlightCount, lastProcessingDurationMilliseconds, oldestPendingEventAge, 0)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsSnapshot" /> 类型的实例。
    /// </summary>
    /// <param name="consumerName">逻辑消费者名称。</param>
    /// <param name="pendingStreams">最近一次扫描发现的待处理流数。</param>
    /// <param name="pendingEvents">最近一次统计发现的待处理事件数。</param>
    /// <param name="processedCount">成功处理的事件数。</param>
    /// <param name="gapCount">发现前序缺口的次数。</param>
    /// <param name="failureCount">业务处理失败的次数。</param>
    /// <param name="retryCount">恢复事务已确认提交的重试次数。</param>
    /// <param name="inFlightCount">当前正在处理的事件数。</param>
    /// <param name="lastProcessingDurationMilliseconds">最近一次处理耗时，单位为毫秒。</param>
    /// <param name="oldestPendingEventAge">最老待处理事件年龄；没有统计数据时返回 <see langword="null"/>。</param>
    /// <param name="scanFailureCount">扫描或积压统计失败次数。</param>
    public BingOrderedEventMetricsSnapshot(string consumerName, long pendingStreams, long pendingEvents,
        long processedCount, long gapCount, long failureCount, long retryCount, long inFlightCount,
        long lastProcessingDurationMilliseconds, TimeSpan? oldestPendingEventAge, long scanFailureCount)
    {
        ConsumerName = consumerName;
        PendingStreams = pendingStreams;
        PendingEvents = pendingEvents;
        ProcessedCount = processedCount;
        GapCount = gapCount;
        FailureCount = failureCount;
        RetryCount = retryCount;
        InFlightCount = inFlightCount;
        LastProcessingDurationMilliseconds = lastProcessingDurationMilliseconds;
        OldestPendingEventAge = oldestPendingEventAge;
        ScanFailureCount = Math.Max(0, scanFailureCount);
    }

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsSnapshot" /> 类型的实例。
    /// </summary>
    /// <param name="consumerName">逻辑消费者名称。</param>
    /// <param name="pendingStreams">最近一次扫描发现的待处理流数。</param>
    /// <param name="pendingEvents">最近一次统计发现的待处理事件数。</param>
    /// <param name="processedCount">成功处理的事件数。</param>
    /// <param name="gapCount">发现前序缺口的次数。</param>
    /// <param name="failureCount">业务处理失败的次数。</param>
    /// <param name="retryCount">恢复事务已确认提交的重试次数。</param>
    /// <param name="inFlightCount">当前正在处理的事件数。</param>
    /// <param name="lastProcessingDurationMilliseconds">最近一次处理耗时，单位为毫秒。</param>
    public BingOrderedEventMetricsSnapshot(string consumerName, long pendingStreams, long pendingEvents,
        long processedCount, long gapCount, long failureCount, long retryCount, long inFlightCount,
        long lastProcessingDurationMilliseconds)
        : this(consumerName, pendingStreams, pendingEvents, processedCount, gapCount, failureCount,
            retryCount, inFlightCount, lastProcessingDurationMilliseconds, null)
    {
    }

    /// <summary>
    /// 获取逻辑消费者名称。
    /// </summary>
    public string ConsumerName { get; }

    /// <summary>
    /// 获取最近一次扫描发现的待处理流数。
    /// </summary>
    public long PendingStreams { get; }

    /// <summary>
    /// 获取最近一次统计发现的待处理事件数。
    /// </summary>
    public long PendingEvents { get; }

    /// <summary>
    /// 获取成功处理的事件数。
    /// </summary>
    public long ProcessedCount { get; }

    /// <summary>
    /// 获取发现前序缺口的次数。
    /// </summary>
    public long GapCount { get; }

    /// <summary>
    /// 获取业务处理失败的次数。
    /// </summary>
    public long FailureCount { get; }

    /// <summary>
    /// 获取恢复事务已确认提交的重试次数。
    /// </summary>
    public long RetryCount { get; }

    /// <summary>
    /// 获取当前正在处理的事件数。
    /// </summary>
    public long InFlightCount { get; }

    /// <summary>
    /// 获取最近一次处理耗时，单位为毫秒。
    /// </summary>
    public long LastProcessingDurationMilliseconds { get; }

    /// <summary>
    /// 获取最老待处理事件的年龄。
    /// </summary>
    public TimeSpan? OldestPendingEventAge { get; }

    /// <summary>
    /// 获取扫描或积压统计失败次数。
    /// </summary>
    public long ScanFailureCount { get; }
}

/// <summary>
/// 使用原子计数收集单个有序消费者的运行指标。
/// </summary>
public sealed class BingOrderedEventMetrics : IOrderedEventMetrics, IDisposable
{
    /// <summary>
    /// 有序事件指标的 Meter 名称。
    /// </summary>
    public const string MeterName = "Bing.Events.Ordering";

    /// <summary>
    /// 消费者标签名称。
    /// </summary>
    public const string ConsumerTagName = "consumer";

    /// <summary>
    /// 当前消费者的指标 Meter。
    /// </summary>
    private readonly Meter _meter;

    /// <summary>
    /// 当前消费者的固定低基数标签。
    /// </summary>
    private readonly KeyValuePair<string, object> _consumerTag;

    /// <summary>
    /// 指示指标 Meter 是否已经释放。
    /// </summary>
    private int _disposed;

    /// <summary>
    /// 当前待处理业务流数量。
    /// </summary>
    private long _pendingStreams;

    /// <summary>
    /// 当前待处理事件数量。
    /// </summary>
    private long _pendingEvents;

    /// <summary>
    /// 已成功处理的事件数量。
    /// </summary>
    private long _processedCount;

    /// <summary>
    /// 发现前序缺口的次数。
    /// </summary>
    private long _gapCount;

    /// <summary>
    /// 业务处理失败的次数。
    /// </summary>
    private long _failureCount;

    /// <summary>
    /// 扫描或积压统计失败次数。
    /// </summary>
    private long _scanFailureCount;

    /// <summary>
    /// 已确认提交的恢复重试次数。
    /// </summary>
    private long _retryCount;

    /// <summary>
    /// 当前正在处理的事件数量。
    /// </summary>
    private long _inFlightCount;

    /// <summary>
    /// 最近一次处理耗时，单位为毫秒。
    /// </summary>
    private long _lastProcessingDurationMilliseconds;

    /// <summary>
    /// 最老待处理事件年龄，单位为毫秒；负一表示没有统计值。
    /// </summary>
    private long _oldestPendingEventAgeMilliseconds = -1;

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetrics" /> 类型的实例。
    /// </summary>
    /// <param name="consumerName">逻辑消费者名称。</param>
    public BingOrderedEventMetrics(string consumerName)
    {
        OrderedEventConsumerName.Validate(consumerName, nameof(consumerName));
        ConsumerName = consumerName;
        _consumerTag = new KeyValuePair<string, object>(ConsumerTagName, consumerName);
        _meter = new Meter(MeterName);
        RegisterInstruments();
    }

    /// <inheritdoc />
    public string ConsumerName { get; }

    /// <inheritdoc />
    public BingOrderedEventMetricsSnapshot Snapshot => new(ConsumerName,
        Interlocked.Read(ref _pendingStreams), Interlocked.Read(ref _pendingEvents),
        Interlocked.Read(ref _processedCount),
        Interlocked.Read(ref _gapCount), Interlocked.Read(ref _failureCount),
        Interlocked.Read(ref _retryCount), Interlocked.Read(ref _inFlightCount),
        Interlocked.Read(ref _lastProcessingDurationMilliseconds),
        ToAge(Interlocked.Read(ref _oldestPendingEventAgeMilliseconds)),
        Interlocked.Read(ref _scanFailureCount));

    /// <summary>
    /// 记录最近一次扫描的待处理流数。
    /// </summary>
    /// <param name="count">待处理流数量，小于零时按零记录。</param>
    public void RecordPendingStreams(long count) => Interlocked.Exchange(ref _pendingStreams, Math.Max(0, count));

    /// <summary>
    /// 记录最近一次统计的待处理事件数。
    /// </summary>
    /// <param name="count">待处理事件数量，小于零时按零记录。</param>
    public void RecordPendingEvents(long count) => Interlocked.Exchange(ref _pendingEvents, Math.Max(0, count));

    /// <summary>
    /// 记录最老待处理事件年龄。
    /// </summary>
    /// <param name="age">事件年龄；没有积压或无法统计时传入 <see langword="null"/>。</param>
    public void RecordOldestPendingEventAge(TimeSpan? age) =>
        Interlocked.Exchange(ref _oldestPendingEventAgeMilliseconds, age.HasValue
            ? ToMilliseconds(age.Value)
            : -1);

    /// <summary>
    /// 记录一次成功处理。
    /// </summary>
    /// <param name="duration">本次处理耗时。</param>
    public void RecordProcessed(TimeSpan duration)
    {
        Interlocked.Increment(ref _processedCount);
        Interlocked.Exchange(ref _lastProcessingDurationMilliseconds, ToMilliseconds(duration));
    }

    /// <summary>
    /// 记录一次前序缺口。
    /// </summary>
    public void RecordGap() => Interlocked.Increment(ref _gapCount);

    /// <summary>
    /// 记录一次业务失败。
    /// </summary>
    /// <param name="duration">本次处理耗时。</param>
    public void RecordFailure(TimeSpan duration)
    {
        Interlocked.Increment(ref _failureCount);
        Interlocked.Exchange(ref _lastProcessingDurationMilliseconds, ToMilliseconds(duration));
    }

    /// <summary>
    /// 记录一次扫描或积压统计失败。
    /// </summary>
    public void RecordScanFailure() => Interlocked.Increment(ref _scanFailureCount);

    /// <summary>
    /// 记录一次已确认提交的恢复重试。
    /// </summary>
    public void RecordRetry() => Interlocked.Increment(ref _retryCount);

    /// <summary>
    /// 登记一次处理开始。
    /// </summary>
    public void RecordStarted() => Interlocked.Increment(ref _inFlightCount);

    /// <summary>
    /// 登记一次处理完成。
    /// </summary>
    public void RecordCompleted()
    {
        while (true)
        {
            var current = Interlocked.Read(ref _inFlightCount);
            if (current <= 0 || Interlocked.CompareExchange(ref _inFlightCount, current - 1, current) == current)
                return;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _meter.Dispose();
    }

    /// <summary>
    /// 注册可由 OpenTelemetry、Prometheus 或其他 MeterListener 读取的指标。
    /// </summary>
    private void RegisterInstruments()
    {
        _meter.CreateObservableGauge("bing.ordered.pending.streams",
            () => new Measurement<long>(Interlocked.Read(ref _pendingStreams), _consumerTag));
        _meter.CreateObservableGauge("bing.ordered.pending.events",
            () => new Measurement<long>(Interlocked.Read(ref _pendingEvents), _consumerTag));
        _meter.CreateObservableGauge("bing.ordered.pending.age.ms",
            ObserveOldestPendingEventAge);
        _meter.CreateObservableGauge("bing.ordered.inflight",
            () => new Measurement<long>(Interlocked.Read(ref _inFlightCount), _consumerTag));
        _meter.CreateObservableGauge("bing.ordered.last.duration.ms",
            () => new Measurement<long>(Interlocked.Read(ref _lastProcessingDurationMilliseconds), _consumerTag));
        _meter.CreateObservableCounter("bing.ordered.processed",
            () => new Measurement<long>(Interlocked.Read(ref _processedCount), _consumerTag));
        _meter.CreateObservableCounter("bing.ordered.gaps",
            () => new Measurement<long>(Interlocked.Read(ref _gapCount), _consumerTag));
        _meter.CreateObservableCounter("bing.ordered.failures",
            () => new Measurement<long>(Interlocked.Read(ref _failureCount), _consumerTag));
        _meter.CreateObservableCounter("bing.ordered.scan.failures",
            () => new Measurement<long>(Interlocked.Read(ref _scanFailureCount), _consumerTag));
        _meter.CreateObservableCounter("bing.ordered.retries",
            () => new Measurement<long>(Interlocked.Read(ref _retryCount), _consumerTag));
    }

    /// <summary>
    /// 将耗时转换为非负毫秒数。
    /// </summary>
    /// <param name="duration">待转换的处理耗时。</param>
    /// <returns>向上取整后的非负毫秒数。</returns>
    private static long ToMilliseconds(TimeSpan duration) =>
        Math.Max(0, (long)Math.Ceiling(duration.TotalMilliseconds));

    /// <summary>
    /// 将内部年龄毫秒值转换为公开值。
    /// </summary>
    /// <param name="milliseconds">内部年龄毫秒值。</param>
    /// <returns>公开年龄；负一返回 <see langword="null"/>。</returns>
    private static TimeSpan? ToAge(long milliseconds) => milliseconds < 0
        ? null
        : TimeSpan.FromMilliseconds(milliseconds);

    /// <summary>
    /// 观察最老积压年龄；没有有效统计值时不产生测量。
    /// </summary>
    /// <returns>当前消费者的年龄测量。</returns>
    private IEnumerable<Measurement<long>> ObserveOldestPendingEventAge()
    {
        var milliseconds = Interlocked.Read(ref _oldestPendingEventAgeMilliseconds);
        return milliseconds < 0
            ? Array.Empty<Measurement<long>>()
            : new[] { new Measurement<long>(milliseconds, _consumerTag) };
    }
}

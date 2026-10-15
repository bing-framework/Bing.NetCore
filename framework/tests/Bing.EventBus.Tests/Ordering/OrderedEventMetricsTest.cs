using Bing.Events.Ordering;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.Metrics;

namespace Bing.EventBus.Tests.Ordering;

/// <summary>
/// 验证有序消费者指标的原子计数和快照隔离。
/// </summary>
public class OrderedEventMetricsTest
{
    /// <summary>
    /// 记录各类事件后快照反映聚合值。
    /// </summary>
    [Fact]
    public void Snapshot_ShouldExposeAggregatedCounters()
    {
        var metrics = new BingOrderedEventMetrics("orders");
        metrics.RecordPendingStreams(3);
        metrics.RecordPendingEvents(7);
        metrics.RecordOldestPendingEventAge(TimeSpan.FromSeconds(3));
        metrics.RecordStarted();
        metrics.RecordProcessed(TimeSpan.FromMilliseconds(2));
        metrics.RecordCompleted();
        metrics.RecordGap();
        metrics.RecordFailure(TimeSpan.FromMilliseconds(5));
        metrics.RecordRetry();
        metrics.RecordScanFailure();

        var snapshot = metrics.Snapshot;
        snapshot.ConsumerName.ShouldBe("orders");
        snapshot.PendingStreams.ShouldBe(3);
        snapshot.PendingEvents.ShouldBe(7);
        snapshot.ProcessedCount.ShouldBe(1);
        snapshot.GapCount.ShouldBe(1);
        snapshot.FailureCount.ShouldBe(1);
        snapshot.RetryCount.ShouldBe(1);
        snapshot.ScanFailureCount.ShouldBe(1);
        snapshot.InFlightCount.ShouldBe(0);
        snapshot.LastProcessingDurationMilliseconds.ShouldBe(5);
        snapshot.OldestPendingEventAge.ShouldBe(TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// 重复完成不会让指标出现负的在途数量。
    /// </summary>
    [Fact]
    public void Snapshot_WhenCompletionIsRepeated_ShouldKeepNonNegativeInFlight()
    {
        var metrics = new BingOrderedEventMetrics("orders");
        metrics.RecordCompleted();

        metrics.Snapshot.InFlightCount.ShouldBe(0);
    }

    /// <summary>
    /// 没有积压年龄时快照应保持空值。
    /// </summary>
    [Fact]
    public void Snapshot_WhenPendingAgeIsMissing_ShouldReturnNull()
    {
        var metrics = new BingOrderedEventMetrics("orders");

        metrics.Snapshot.OldestPendingEventAge.ShouldBeNull();
        metrics.RecordOldestPendingEventAge(TimeSpan.FromMilliseconds(-1));
        metrics.Snapshot.OldestPendingEventAge.ShouldBe(TimeSpan.Zero);
        metrics.RecordOldestPendingEventAge(null);
        metrics.Snapshot.OldestPendingEventAge.ShouldBeNull();
    }

    /// <summary>
    /// 逻辑消费者名称不能为空。
    /// </summary>
    [Fact]
    public void Constructor_WhenConsumerIsMissing_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new BingOrderedEventMetrics(" "));
    }

    /// <summary>
    /// 标准 Meter 应公开固定消费者标签和聚合指标。
    /// </summary>
    [Fact]
    public void Metrics_ShouldExposeObservableMeasurements()
    {
        var consumer = $"metrics-{Guid.NewGuid():N}";
        var values = new Dictionary<string, long>(StringComparer.Ordinal);

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BingOrderedEventMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == BingOrderedEventMetrics.ConsumerTagName && Equals(tag.Value, consumer))
                    values[instrument.Name] = measurement;
            }
        });
        listener.Start();

        using var metrics = new BingOrderedEventMetrics(consumer);
        metrics.RecordPendingStreams(2);
        metrics.RecordPendingEvents(4);
        metrics.RecordOldestPendingEventAge(TimeSpan.FromSeconds(3));
        metrics.RecordStarted();
        metrics.RecordProcessed(TimeSpan.FromMilliseconds(7));
        metrics.RecordCompleted();
        metrics.RecordScanFailure();

        listener.RecordObservableInstruments();

        values["bing.ordered.pending.streams"].ShouldBe(2);
        values["bing.ordered.pending.events"].ShouldBe(4);
        values["bing.ordered.pending.age.ms"].ShouldBe(3000);
        values["bing.ordered.inflight"].ShouldBe(0);
        values["bing.ordered.last.duration.ms"].ShouldBe(7);
        values["bing.ordered.processed"].ShouldBe(1);
        values["bing.ordered.scan.failures"].ShouldBe(1);
    }

    /// <summary>
    /// 没有积压年龄时 Meter 不应上报负数哨兵值。
    /// </summary>
    [Fact]
    public void Metrics_WhenPendingAgeIsMissing_ShouldNotPublishAgeMeasurement()
    {
        var consumer = $"metrics-missing-age-{Guid.NewGuid():N}";
        var values = new List<string>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BingOrderedEventMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == BingOrderedEventMetrics.ConsumerTagName && Equals(tag.Value, consumer))
                    values.Add(instrument.Name);
            }
        });
        listener.Start();

        using var metrics = new BingOrderedEventMetrics(consumer);
        listener.RecordObservableInstruments();

        values.ShouldNotContain("bing.ordered.pending.age.ms");
    }

    /// <summary>
    /// Prometheus 导出应保持稳定顺序、低基数标签和完整指标类型。
    /// </summary>
    [Fact]
    public void PrometheusExporter_ShouldWriteStableLowCardinalityText()
    {
        using var metrics = new BingOrderedEventMetrics("orders\"\nblue");
        metrics.RecordPendingStreams(2);
        metrics.RecordPendingEvents(5);
        metrics.RecordOldestPendingEventAge(TimeSpan.FromMilliseconds(2.2));
        metrics.RecordStarted();
        metrics.RecordProcessed(TimeSpan.FromMilliseconds(7));

        var provider = new BingOrderedEventMetricsSnapshotProvider(new[] { metrics });
        var output = BingOrderedEventMetricsPrometheusExporter.Export(provider);

        output.ShouldContain("# TYPE bing_ordered_pending_streams gauge\n");
        output.ShouldContain("bing_ordered_pending_streams{consumer=\"orders\\\"\\nblue\"} 2\n");
        output.ShouldContain("bing_ordered_pending_events{consumer=\"orders\\\"\\nblue\"} 5\n");
        output.ShouldContain("bing_ordered_pending_age_ms{consumer=\"orders\\\"\\nblue\"} 3\n");
        output.ShouldContain("bing_ordered_processed_total{consumer=\"orders\\\"\\nblue\"} 1\n");
        output.ShouldContain("# TYPE bing_ordered_scan_failures_total counter\n");
        output.ShouldNotContain("event-id");
        output.ShouldNotContain("entity-key");
    }

    /// <summary>
    /// Prometheus 导出应拒绝重复的消费者指标身份。
    /// </summary>
    [Fact]
    public void PrometheusExporter_WhenProviderReturnsDuplicateNames_ShouldReject()
    {
        var provider = new DuplicateSnapshotProvider();

        var exception = Should.Throw<InvalidOperationException>(() =>
            BingOrderedEventMetricsPrometheusExporter.Export(provider));

        exception.Message.ShouldContain("orders");
    }

    /// <summary>
    /// Prometheus 导出应拒绝空的快照提供程序。
    /// </summary>
    [Fact]
    public void PrometheusExporter_WhenProviderIsMissing_ShouldReject()
    {
        Should.Throw<ArgumentNullException>(() =>
            BingOrderedEventMetricsPrometheusExporter.Export(null));
    }

    /// <summary>
    /// Prometheus 导出应拒绝空的快照集合。
    /// </summary>
    [Fact]
    public void PrometheusExporter_WhenProviderReturnsNull_ShouldReject()
    {
        Should.Throw<InvalidOperationException>(() =>
            BingOrderedEventMetricsPrometheusExporter.Export(new NullSnapshotProvider()));
    }

    /// <summary>
    /// Prometheus 导出应拒绝包含空元素的快照集合。
    /// </summary>
    [Fact]
    public void PrometheusExporter_WhenProviderReturnsNullSnapshot_ShouldReject()
    {
        Should.Throw<InvalidOperationException>(() =>
            BingOrderedEventMetricsPrometheusExporter.Export(new NullElementSnapshotProvider()));
    }

    /// <summary>
    /// Prometheus 导出应拒绝非法的消费者身份。
    /// </summary>
    [Fact]
    public void PrometheusExporter_WhenConsumerNameIsInvalid_ShouldReject()
    {
        Should.Throw<ArgumentException>(() =>
            BingOrderedEventMetricsPrometheusExporter.Export(new InvalidNameSnapshotProvider()));
    }

    /// <summary>
    /// 快照提供程序应按序返回各消费者，并且不允许调用方修改结果集合。
    /// </summary>
    [Fact]
    public void SnapshotProvider_ShouldReturnStableReadOnlySnapshots()
    {
        var zulu = new BingOrderedEventMetrics("zulu");
        var alpha = new BingOrderedEventMetrics("alpha");
        alpha.RecordProcessed(TimeSpan.FromMilliseconds(4));
        var provider = new BingOrderedEventMetricsSnapshotProvider(new IOrderedEventMetrics[] { zulu, alpha });

        var snapshots = provider.GetSnapshots();

        snapshots.Select(item => item.ConsumerName).ShouldBe(new[] { "alpha", "zulu" });
        snapshots[0].ProcessedCount.ShouldBe(1);
        Should.Throw<NotSupportedException>(() => ((IList<BingOrderedEventMetricsSnapshot>)snapshots).Clear());

        alpha.RecordProcessed(TimeSpan.FromMilliseconds(8));
        provider.GetSnapshots()[0].ProcessedCount.ShouldBe(2);
    }

    /// <summary>
    /// 重复消费者名称不能被诊断入口静默合并。
    /// </summary>
    [Fact]
    public void SnapshotProvider_WhenConsumerNamesDuplicate_ShouldReject()
    {
        var first = new StubMetrics("orders");
        var second = new StubMetrics("orders");

        var exception = Should.Throw<InvalidOperationException>(() =>
            new BingOrderedEventMetricsSnapshotProvider(new[] { first, second }));

        exception.Message.ShouldContain("orders");
    }

    /// <summary>
    /// 自定义指标返回的快照名称不一致时应立即报告契约错误。
    /// </summary>
    [Fact]
    public void SnapshotProvider_WhenSnapshotIdentityDiffers_ShouldReject()
    {
        var provider = new BingOrderedEventMetricsSnapshotProvider(new[]
        {
            new StubMetrics("orders", new BingOrderedEventMetricsSnapshot("other", 0, 0, 0, 0, 0, 0, 0, 0))
        });

        var exception = Should.Throw<InvalidOperationException>(() => provider.GetSnapshots());

        exception.Message.ShouldContain("不一致");
    }

    /// <summary>
    /// 自定义指标的消费者名称必须符合有序消费者身份约束。
    /// </summary>
    [Fact]
    public void SnapshotProvider_WhenConsumerNameIsInvalid_ShouldReject()
    {
        Should.Throw<ArgumentException>(() =>
            new BingOrderedEventMetricsSnapshotProvider(new[] { new StubMetrics(" orders") }));
    }

    /// <summary>
    /// 诊断服务注册应保持幂等并尊重调用方的自定义实现。
    /// </summary>
    [Fact]
    public void AddDiagnostics_ShouldKeepExistingProvider()
    {
        var services = new ServiceCollection();
        var existing = new StubSnapshotProvider();
        services.AddSingleton<IOrderedEventMetricsSnapshotProvider>(existing);

        services.AddBingOrderedEventMetricsDiagnostics();
        services.AddBingOrderedEventMetricsDiagnostics();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOrderedEventMetricsSnapshotProvider>().ShouldBeSameAs(existing);
        services.Count(item => item.ServiceType == typeof(IOrderedEventMetricsSnapshotProvider)).ShouldBe(1);
    }

    /// <summary>
    /// 默认诊断提供程序没有消费者时应返回空快照。
    /// </summary>
    [Fact]
    public void AddDiagnostics_WhenNoConsumerIsRegistered_ShouldReturnEmptySnapshot()
    {
        var services = new ServiceCollection();
        services.AddBingOrderedEventMetricsDiagnostics();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOrderedEventMetricsSnapshotProvider>()
            .GetSnapshots()
            .ShouldBeEmpty();
    }

    /// <summary>
    /// 验证指标快照的最小自定义来源。
    /// </summary>
    private sealed class StubMetrics : IOrderedEventMetrics
    {
        /// <summary>
        /// 初始化一个 <see cref="StubMetrics" /> 类型的实例。
        /// </summary>
        /// <param name="consumerName">逻辑消费者名称。</param>
        /// <param name="snapshot">返回的指标快照。</param>
        public StubMetrics(string consumerName, BingOrderedEventMetricsSnapshot snapshot = null)
        {
            ConsumerName = consumerName;
            Snapshot = snapshot ?? new BingOrderedEventMetricsSnapshot(consumerName, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        /// <inheritdoc />
        public string ConsumerName { get; }

        /// <inheritdoc />
        public BingOrderedEventMetricsSnapshot Snapshot { get; }
    }

    /// <summary>
    /// 验证自定义诊断提供程序保留行为。
    /// </summary>
    private sealed class StubSnapshotProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() =>
            Array.Empty<BingOrderedEventMetricsSnapshot>();
    }

    /// <summary>
    /// 返回重复消费者快照的测试提供程序。
    /// </summary>
    private sealed class DuplicateSnapshotProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() => new[]
        {
            new BingOrderedEventMetricsSnapshot("orders", 0, 0, 0, 0, 0, 0, 0, 0),
            new BingOrderedEventMetricsSnapshot("orders", 0, 0, 0, 0, 0, 0, 0, 0)
        };
    }

    /// <summary>
    /// 返回空快照集合的测试提供程序。
    /// </summary>
    private sealed class NullSnapshotProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() => null;
    }

    /// <summary>
    /// 返回包含空元素快照的测试提供程序。
    /// </summary>
    private sealed class NullElementSnapshotProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() => new BingOrderedEventMetricsSnapshot[] { null };
    }

    /// <summary>
    /// 返回非法消费者身份快照的测试提供程序。
    /// </summary>
    private sealed class InvalidNameSnapshotProvider : IOrderedEventMetricsSnapshotProvider
    {
        /// <inheritdoc />
        public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots() => new[]
        {
            new BingOrderedEventMetricsSnapshot(" orders", 0, 0, 0, 0, 0, 0, 0, 0)
        };
    }
}

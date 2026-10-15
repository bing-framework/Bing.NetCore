using System.Globalization;
using System.Text;

namespace Bing.Events.Ordering;

/// <summary>
/// 将有序消费者指标快照导出为 Prometheus 文本格式。
/// </summary>
/// <remarks>
/// 输出只使用逻辑消费者名称这一项固定低基数标签；该类型不创建 HTTP 端点，也不负责认证和授权。
/// </remarks>
public static class BingOrderedEventMetricsPrometheusExporter
{
    /// <summary>
    /// 导出当前有序消费者指标。
    /// </summary>
    /// <param name="provider">指标快照提供程序。</param>
    /// <returns>Prometheus 0.0.4 文本格式的指标内容。</returns>
    public static string Export(IOrderedEventMetricsSnapshotProvider provider)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        var snapshots = provider.GetSnapshots();
        if (snapshots == null)
            throw new InvalidOperationException("有序消费者指标提供程序不能返回 null。");

        var ordered = snapshots.ToArray();
        if (ordered.Any(snapshot => snapshot == null))
            throw new InvalidOperationException("有序消费者指标快照不能包含 null。");
        foreach (var snapshot in ordered)
            OrderedEventConsumerName.Validate(snapshot.ConsumerName, nameof(provider));

        var duplicate = ordered
            .GroupBy(snapshot => snapshot.ConsumerName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException($"有序消费者指标名称重复：{duplicate.Key}。");

        ordered = ordered.OrderBy(snapshot => snapshot.ConsumerName, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();
        AppendMetric(builder, ordered, "bing_ordered_pending_streams",
            "Number of pending business streams.", "gauge", snapshot => snapshot.PendingStreams);
        AppendMetric(builder, ordered, "bing_ordered_pending_events",
            "Number of pending events.", "gauge", snapshot => snapshot.PendingEvents);
        AppendAgeMetric(builder, ordered);
        AppendMetric(builder, ordered, "bing_ordered_inflight",
            "Number of events currently being processed.", "gauge", snapshot => snapshot.InFlightCount);
        AppendMetric(builder, ordered, "bing_ordered_last_duration_ms",
            "Duration of the most recent event processing in milliseconds.", "gauge",
            snapshot => snapshot.LastProcessingDurationMilliseconds);
        AppendMetric(builder, ordered, "bing_ordered_processed_total",
            "Number of successfully processed events.", "counter", snapshot => snapshot.ProcessedCount);
        AppendMetric(builder, ordered, "bing_ordered_gaps_total",
            "Number of sequence gaps observed.", "counter", snapshot => snapshot.GapCount);
        AppendMetric(builder, ordered, "bing_ordered_failures_total",
            "Number of failed event processing attempts.", "counter", snapshot => snapshot.FailureCount);
        AppendMetric(builder, ordered, "bing_ordered_retries_total",
            "Number of recovery retries committed.", "counter", snapshot => snapshot.RetryCount);
        AppendMetric(builder, ordered, "bing_ordered_scan_failures_total",
            "Number of pending-event scan failures.", "counter", snapshot => snapshot.ScanFailureCount);
        return builder.ToString();
    }

    /// <summary>
    /// 写入一个带消费者标签的整数指标。
    /// </summary>
    private static void AppendMetric(StringBuilder builder,
        IReadOnlyList<BingOrderedEventMetricsSnapshot> snapshots, string name, string help, string type,
        Func<BingOrderedEventMetricsSnapshot, long> valueSelector)
    {
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
        foreach (var snapshot in snapshots)
            AppendValue(builder, name, snapshot.ConsumerName, valueSelector(snapshot));
    }

    /// <summary>
    /// 写入可选的最老积压年龄指标。
    /// </summary>
    private static void AppendAgeMetric(StringBuilder builder,
        IReadOnlyList<BingOrderedEventMetricsSnapshot> snapshots)
    {
        const string name = "bing_ordered_pending_age_ms";
        builder.Append("# HELP ").Append(name)
            .Append(" Age of the oldest pending event in milliseconds.\n");
        builder.Append("# TYPE ").Append(name).Append(" gauge\n");
        foreach (var snapshot in snapshots)
        {
            if (!snapshot.OldestPendingEventAge.HasValue)
                continue;
            var milliseconds = Math.Max(0, (long)Math.Ceiling(
                snapshot.OldestPendingEventAge.Value.TotalMilliseconds));
            AppendValue(builder, name, snapshot.ConsumerName, milliseconds);
        }
    }

    /// <summary>
    /// 写入一行指标值。
    /// </summary>
    private static void AppendValue(StringBuilder builder, string name, string consumerName, long value)
    {
        builder.Append(name).Append("{consumer=\"")
            .Append(EscapeLabelValue(consumerName)).Append("\"} ")
            .Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    /// <summary>
    /// 转义 Prometheus 标签值中的特殊字符。
    /// </summary>
    private static string EscapeLabelValue(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");
}

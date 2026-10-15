using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bing.Events.Ordering;

/// <summary>
/// 提供当前宿主中有序消费者的只读指标快照。
/// </summary>
public interface IOrderedEventMetricsSnapshotProvider
{
    /// <summary>
    /// 获取按逻辑消费者名称排序的指标快照。
    /// </summary>
    /// <returns>不包含事件正文、事件标识或实体键的指标快照集合。</returns>
    IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots();
}

/// <summary>
/// 汇总当前服务集合中已注册的有序消费者指标。
/// </summary>
/// <remarks>
/// 该类型只提供进程内快照，不执行授权、不暴露 HTTP 端点，也不建立永久静态缓存。
/// 宿主将快照暴露给外部系统时，必须自行配置认证、授权和低基数输出策略。
/// </remarks>
public sealed class BingOrderedEventMetricsSnapshotProvider : IOrderedEventMetricsSnapshotProvider
{
    /// <summary>
    /// 已注册的指标来源。
    /// </summary>
    private readonly IReadOnlyList<IOrderedEventMetrics> _metrics;

    /// <summary>
    /// 初始化一个 <see cref="BingOrderedEventMetricsSnapshotProvider" /> 类型的实例。
    /// </summary>
    /// <param name="metrics">当前宿主注册的指标来源。</param>
    public BingOrderedEventMetricsSnapshotProvider(IEnumerable<IOrderedEventMetrics> metrics)
    {
        if (metrics == null)
            throw new ArgumentNullException(nameof(metrics));

        var sources = metrics.ToArray();
        if (sources.Any(item => item == null))
            throw new ArgumentException("有序消费者指标来源不能包含 null。", nameof(metrics));
        foreach (var source in sources)
            OrderedEventConsumerName.Validate(source.ConsumerName, nameof(metrics));

        var duplicate = sources
            .GroupBy(item => item.ConsumerName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException($"有序消费者指标名称重复：{duplicate.Key}。");

        _metrics = sources
            .OrderBy(item => item.ConsumerName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<BingOrderedEventMetricsSnapshot> GetSnapshots()
    {
        var snapshots = new BingOrderedEventMetricsSnapshot[_metrics.Count];
        for (var index = 0; index < _metrics.Count; index++)
        {
            var source = _metrics[index];
            var snapshot = source.Snapshot ?? throw new InvalidOperationException(
                $"有序消费者指标 {source.ConsumerName} 返回了 null 快照。");
            if (!string.Equals(source.ConsumerName, snapshot.ConsumerName, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"有序消费者指标名称与快照不一致：{source.ConsumerName}、{snapshot.ConsumerName}。");
            snapshots[index] = snapshot;
        }

        return new ReadOnlyCollection<BingOrderedEventMetricsSnapshot>(snapshots);
    }
}

/// <summary>
/// 注册有序消费者指标诊断服务。
/// </summary>
public static class OrderedEventMetricsDiagnosticsExtensions
{
    /// <summary>
    /// 注册有序消费者指标只读快照提供程序。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <returns>原服务集合。</returns>
    /// <remarks>重复调用不会覆盖调用方已有的快照提供程序。</remarks>
    public static IServiceCollection AddBingOrderedEventMetricsDiagnostics(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<IOrderedEventMetricsSnapshotProvider,
            BingOrderedEventMetricsSnapshotProvider>();
        return services;
    }
}

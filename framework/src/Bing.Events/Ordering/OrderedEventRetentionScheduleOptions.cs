using System.Collections.Generic;

namespace Bing.Events.Ordering;

/// <summary>
/// 配置有序事件收件箱留存清理调度。
/// </summary>
public sealed class OrderedEventRetentionScheduleOptions
{
    /// <summary>
    /// 是否启用后台清理。
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 两次清理尝试之间的间隔。
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// 已处理收件记录的保留时长。
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// 已归档收件记录的保留时长。
    /// </summary>
    /// <remarks>为空时不自动清理历史归档。</remarks>
    public TimeSpan? ArchiveRetentionPeriod { get; set; }

    /// <summary>
    /// 每次清理最多删除的记录数。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 启动后是否立即执行一次清理。
    /// </summary>
    public bool RunImmediately { get; set; }

    /// <summary>
    /// 需要清理的稳定逻辑消费者名称。
    /// </summary>
    /// <remarks>消费者名称必须与有序消费者注册时使用的名称一致。</remarks>
    public IList<string> Consumers { get; } = new List<string>();

    /// <summary>
    /// 需要自动清理归档记录的逻辑消费者名称。
    /// </summary>
    /// <remarks>必须同时配置 <see cref="ArchiveRetentionPeriod"/> 才会启用归档清理。</remarks>
    public IList<string> ArchiveConsumers { get; } = new List<string>();

    /// <summary>
    /// 是否使用协调租约避免多个节点同时清理。
    /// </summary>
    public bool UseCoordinationLease { get; set; }

    /// <summary>
    /// 清理协调租约的锁键。
    /// </summary>
    public string CoordinationKey { get; set; } = "bing:ordered-event-retention";

    /// <summary>
    /// 获取协调租约时的最长等待时间。
    /// </summary>
    public TimeSpan CoordinationWaitTimeout { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 协调租约的有效时长。
    /// </summary>
    /// <remarks>留空时使用锁提供程序的默认租期；分布式锁应配置有限租期。</remarks>
    public TimeSpan? CoordinationLeaseDuration { get; set; }

    /// <summary>
    /// 校验调度配置。
    /// </summary>
    internal void Validate()
    {
        if (!Enabled)
            return;
        var hasProcessedConsumers = Consumers != null && Consumers.Count != 0;
        var hasArchiveConsumers = ArchiveConsumers != null && ArchiveConsumers.Count != 0;
        if (Interval <= TimeSpan.Zero || Interval == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(Interval), "有序事件清理间隔必须为正数。" );
        if (hasProcessedConsumers &&
            (RetentionPeriod <= TimeSpan.Zero || RetentionPeriod == Timeout.InfiniteTimeSpan))
            throw new ArgumentOutOfRangeException(nameof(RetentionPeriod), "有序事件保留时长必须为正数。" );
        if (BatchSize <= 0 || BatchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "有序事件清理批量必须位于 1 到 10000 之间。" );
        if (!hasProcessedConsumers && !hasArchiveConsumers)
            throw new InvalidOperationException("有序事件清理至少需要配置一个逻辑消费者。" );

        ValidateConsumers(Consumers, nameof(Consumers));

        if (ArchiveRetentionPeriod.HasValue &&
            (ArchiveRetentionPeriod.Value <= TimeSpan.Zero ||
             ArchiveRetentionPeriod.Value == Timeout.InfiniteTimeSpan))
            throw new ArgumentOutOfRangeException(nameof(ArchiveRetentionPeriod),
                "有序事件归档保留时长必须为正数且不能无限期。" );
        if (hasArchiveConsumers != ArchiveRetentionPeriod.HasValue)
            throw new InvalidOperationException(
                "有序事件归档清理必须同时配置 ArchiveRetentionPeriod 和 ArchiveConsumers。" );
        ValidateConsumers(ArchiveConsumers, nameof(ArchiveConsumers));

        if (!UseCoordinationLease)
            return;
        if (string.IsNullOrWhiteSpace(CoordinationKey))
            throw new ArgumentException("有序事件清理协调租约必须配置非空锁键。", nameof(CoordinationKey));
        if (CoordinationWaitTimeout < TimeSpan.Zero ||
            CoordinationWaitTimeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(CoordinationWaitTimeout),
                "有序事件清理协调租约等待时间不能为负数或无限等待。" );
        if (CoordinationLeaseDuration.HasValue &&
            (CoordinationLeaseDuration.Value <= TimeSpan.Zero ||
             CoordinationLeaseDuration.Value == Timeout.InfiniteTimeSpan))
            throw new ArgumentOutOfRangeException(nameof(CoordinationLeaseDuration),
                "有序事件清理协调租期必须为正数且不能无限期。" );
    }

    /// <summary>
    /// 验证消费者名称及其重复配置。
    /// </summary>
    private static void ValidateConsumers(IList<string> consumers, string name)
    {
        if (consumers == null)
            return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var consumer in consumers)
        {
            OrderedEventConsumerName.Validate(consumer, name);
            if (!names.Add(consumer))
                throw new ArgumentException("有序事件清理不能重复配置同一逻辑消费者。", name);
        }
    }
}

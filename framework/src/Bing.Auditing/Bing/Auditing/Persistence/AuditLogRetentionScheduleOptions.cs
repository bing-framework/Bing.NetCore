using System;

namespace Bing.Auditing.Persistence;

/// <summary>
/// 配置审计日志保留清理调度。
/// </summary>
public sealed class AuditLogRetentionScheduleOptions
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
    /// 普通审计日志的保留时长。
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// 每次清理最多删除的记录数。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 启动后是否立即执行一次清理。
    /// </summary>
    public bool RunImmediately { get; set; }

    /// <summary>
    /// 是否使用协调租约避免多个节点同时清理。
    /// </summary>
    public bool UseCoordinationLease { get; set; }

    /// <summary>
    /// 清理协调租约的锁键。
    /// </summary>
    public string CoordinationKey { get; set; } = "bing:audit-retention";

    /// <summary>
    /// 获取协调租约时的最长等待时间。
    /// </summary>
    public TimeSpan CoordinationWaitTimeout { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 协调租约的有效时长。
    /// </summary>
    /// <remarks>
    /// 留空时使用锁提供程序的默认租期。本地锁提供程序应保持留空，
    /// 分布式锁提供程序可通过配置指定有限租期。
    /// </remarks>
    public TimeSpan? CoordinationLeaseDuration { get; set; }

    /// <summary>
    /// 校验调度配置。
    /// </summary>
    internal void Validate()
    {
        if (!Enabled)
            return;
        if (Interval <= TimeSpan.Zero || Interval == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(Interval), "审计清理间隔必须为正数。" );
        if (RetentionPeriod <= TimeSpan.Zero || RetentionPeriod == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(RetentionPeriod), "审计保留时长必须为正数。" );
        if (BatchSize <= 0 || BatchSize > 10000)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), "审计清理批量必须位于 1 到 10000 之间。" );
        if (!UseCoordinationLease)
            return;
        if (string.IsNullOrWhiteSpace(CoordinationKey))
            throw new ArgumentException("审计清理协调租约必须配置非空锁键。", nameof(CoordinationKey));
        if (CoordinationWaitTimeout < TimeSpan.Zero ||
            CoordinationWaitTimeout == Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(CoordinationWaitTimeout),
                "审计清理协调租约等待时间不能为负数或无限等待。" );
        if (CoordinationLeaseDuration.HasValue &&
            (CoordinationLeaseDuration.Value <= TimeSpan.Zero ||
             CoordinationLeaseDuration.Value == Timeout.InfiniteTimeSpan))
            throw new ArgumentOutOfRangeException(nameof(CoordinationLeaseDuration),
                "审计清理协调租期必须为正数且不能无限期。" );
    }
}

namespace Bing.EasyCaching;

/// <summary>
/// Hybrid 失效消息投递策略。
/// </summary>
/// <remarks>
/// 默认值保持现有行为：只尝试一次且不启用 Redis outbox。只有显式设置
/// <see cref="MaxPendingMessages"/> 后，注册入口才会创建持久化待发队列。
/// </remarks>
public sealed class HybridInvalidationDeliveryOptions
{
    /// <summary>
    /// 获取或设置单条失效消息的发布尝试上限。
    /// </summary>
    public int MaxPublishAttempts { get; set; } = 1;

    /// <summary>
    /// 获取或设置发布失败后的重试间隔。
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// 获取或设置Redis outbox 的待处理消息上限。
    /// </summary>
    /// <remarks>0 表示关闭 outbox。</remarks>
    public int MaxPendingMessages { get; set; }

    /// <summary>
    /// 获取或设置单条 outbox 消息的投递次数上限。
    /// </summary>
    /// <remarks>0 表示不限制。</remarks>
    public int MaxDeliveryAttempts { get; set; }

    /// <summary>
    /// 获取或设置dead-letter 消息数量上限。
    /// </summary>
    /// <remarks>必须与 <see cref="MaxDeliveryAttempts"/> 同时启用。</remarks>
    public int DeadLetterMaxMessages { get; set; }

    /// <summary>
    /// 获取或设置dead-letter JSON payload 的 UTF-8 字节上限。
    /// </summary>
    /// <remarks>按 UTF-8 字节计量，0 表示不限制。</remarks>
    public long DeadLetterMaxBytes { get; set; }

    /// <summary>
    /// 获取或设置dead-letter 消息的保留时长。
    /// </summary>
    /// <remarks>0 表示不自动过期。</remarks>
    public TimeSpan DeadLetterRetention { get; set; }

    /// <summary>
    /// 获取或设置dead-letter 的自动清理间隔。
    /// </summary>
    public TimeSpan DeadLetterCleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 获取或设置每轮 dead-letter 清理数量上限。
    /// </summary>
    public int DeadLetterCleanupBatchSize { get; set; } = 100;

    /// <summary>
    /// 获取或设置outbox 后台投递的轮询间隔。
    /// </summary>
    public TimeSpan OutboxPollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 创建经过校验的配置副本。
    /// </summary>
    /// <returns>配置值副本。</returns>
    internal HybridInvalidationDeliveryOptions CloneValidated()
    {
        if (MaxPublishAttempts <= 0 || MaxPublishAttempts > 32)
            throw new ArgumentOutOfRangeException(nameof(MaxPublishAttempts),
                "Hybrid 失效消息发布尝试次数必须在 1 到 32 之间。");
        if (RetryDelay < TimeSpan.Zero || RetryDelay > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(RetryDelay),
                "Hybrid 失效消息重试间隔必须在 0 到 1 分钟之间。");
        if (MaxPendingMessages < 0 || MaxPendingMessages > 100_000)
            throw new ArgumentOutOfRangeException(nameof(MaxPendingMessages),
                "Hybrid Redis outbox 队列上限必须在 0 到 100000 之间。");
        if (MaxDeliveryAttempts < 0 || MaxDeliveryAttempts > 100_000)
            throw new ArgumentOutOfRangeException(nameof(MaxDeliveryAttempts),
                "Hybrid outbox 最大投递尝试次数必须在 0 到 100000 之间。");
        if (DeadLetterMaxMessages < 0 || DeadLetterMaxMessages > 100_000)
            throw new ArgumentOutOfRangeException(nameof(DeadLetterMaxMessages),
                "Hybrid outbox dead-letter 上限必须在 0 到 100000 之间。");
        if (DeadLetterMaxBytes < 0 || DeadLetterMaxBytes > 1_073_741_824)
            throw new ArgumentOutOfRangeException(nameof(DeadLetterMaxBytes),
                "Hybrid outbox dead-letter 字节上限必须在 0 到 1 GiB 之间。");
        if ((MaxDeliveryAttempts == 0) != (DeadLetterMaxMessages == 0))
            throw new ArgumentException(
                "MaxDeliveryAttempts 与 DeadLetterMaxMessages 必须同时为零或同时为正值。");
        if (DeadLetterMaxBytes > 0 &&
            (MaxDeliveryAttempts == 0 || DeadLetterMaxMessages == 0))
            throw new ArgumentException(
                "启用 dead-letter 字节上限时必须同时启用 MaxDeliveryAttempts 与 DeadLetterMaxMessages。");
        if (DeadLetterRetention < TimeSpan.Zero ||
            DeadLetterRetention > TimeSpan.FromDays(365))
            throw new ArgumentOutOfRangeException(nameof(DeadLetterRetention),
                "Hybrid outbox dead-letter 保留期必须在 0 到 365 天之间。");
        if (DeadLetterRetention > TimeSpan.Zero &&
            (MaxDeliveryAttempts == 0 || DeadLetterMaxMessages == 0))
            throw new ArgumentException(
                "启用 dead-letter 保留期时必须同时启用 MaxDeliveryAttempts 与 DeadLetterMaxMessages。");
        if (DeadLetterCleanupInterval <= TimeSpan.Zero ||
            DeadLetterCleanupInterval > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(DeadLetterCleanupInterval),
                "Hybrid outbox dead-letter 清理间隔必须大于零且不超过 1 天。");
        if (DeadLetterCleanupBatchSize <= 0 || DeadLetterCleanupBatchSize > 10_000)
            throw new ArgumentOutOfRangeException(nameof(DeadLetterCleanupBatchSize),
                "Hybrid outbox dead-letter 清理批大小必须在 1 到 10000 之间。");
        if (OutboxPollInterval <= TimeSpan.Zero || OutboxPollInterval > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(OutboxPollInterval),
                "Hybrid Redis outbox 轮询间隔必须大于零且不超过 1 分钟。");

        return new HybridInvalidationDeliveryOptions
        {
            MaxPublishAttempts = MaxPublishAttempts,
            RetryDelay = RetryDelay,
            MaxPendingMessages = MaxPendingMessages,
            MaxDeliveryAttempts = MaxDeliveryAttempts,
            DeadLetterMaxMessages = DeadLetterMaxMessages,
            DeadLetterMaxBytes = DeadLetterMaxBytes,
            DeadLetterRetention = DeadLetterRetention,
            DeadLetterCleanupInterval = DeadLetterCleanupInterval,
            DeadLetterCleanupBatchSize = DeadLetterCleanupBatchSize,
            OutboxPollInterval = OutboxPollInterval
        };
    }
}

namespace Bing.EasyCaching;

/// <summary>
/// 提供 Hybrid 失效投递器的只读运行状态。
/// </summary>
/// <remarks>
/// 队列/dead-letter 数量、消息年龄和 payload 字节总数来自共享 Redis outbox；计数器和最近失败信息是当前进程视角，进程重启后会清零。
/// 快照各字段可能来自相邻时刻，不构成事务性队列快照。
/// </remarks>
public interface IHybridInvalidationDeliveryDiagnostics
{
    /// <summary>
    /// 读取当前投递状态。
    /// </summary>
    /// <returns>当前进程观察到的投递状态快照。</returns>
    HybridInvalidationDeliverySnapshot GetSnapshot();

    /// <summary>
    /// 异步读取当前投递状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前进程观察到的投递状态快照。</returns>
    Task<HybridInvalidationDeliverySnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示 Hybrid 失效投递器的只读状态快照。
/// </summary>
/// <remarks>各字段可能来自相邻时刻，不构成事务性队列快照。</remarks>
public sealed class HybridInvalidationDeliverySnapshot
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDeliverySnapshot"/> 类型的实例。
    /// </summary>
    /// <param name="topic">失效消息主题。</param>
    /// <param name="outboxEnabled">是否启用 Redis outbox。</param>
    /// <param name="pendingMessages">pending 队列中的消息数。</param>
    /// <param name="processingMessages">processing 队列中的消息数。</param>
    /// <param name="deadLetterMessages">dead-letter 消息数。</param>
    /// <param name="deadLetterBytes">dead-letter payload 的 UTF-8 字节总数。</param>
    /// <param name="oldestPendingAge">pending 中最早消息的年龄。</param>
    /// <param name="oldestProcessingAge">processing 中最早消息的年龄。</param>
    /// <param name="oldestDeadLetterAge">dead-letter 中最早消息的年龄。</param>
    /// <param name="successfulPublishCount">当前进程观察到的成功发布次数。</param>
    /// <param name="failedPublishAttemptCount">当前进程观察到的失败发布次数。</param>
    /// <param name="enqueuedMessageCount">当前进程成功写入 outbox 的消息数。</param>
    /// <param name="deadLetteredMessageCount">当前进程成功隔离到 dead-letter 的消息数。</param>
    /// <param name="expiredDeadLetterCount">当前进程成功清理的 dead-letter 消息数。</param>
    /// <param name="lastFailureUtc">当前进程最近一次失败时间。</param>
    /// <param name="lastFailureMessage">当前进程最近一次失败信息。</param>
    internal HybridInvalidationDeliverySnapshot(string topic, bool outboxEnabled,
        long pendingMessages, long processingMessages, long deadLetterMessages,
        long? deadLetterBytes,
        TimeSpan? oldestPendingAge, TimeSpan? oldestProcessingAge, TimeSpan? oldestDeadLetterAge,
        long successfulPublishCount,
        long failedPublishAttemptCount, long enqueuedMessageCount,
        long deadLetteredMessageCount, long expiredDeadLetterCount, DateTimeOffset? lastFailureUtc,
        string lastFailureMessage)
    {
        Topic = topic;
        OutboxEnabled = outboxEnabled;
        PendingMessages = pendingMessages;
        ProcessingMessages = processingMessages;
        DeadLetterMessages = deadLetterMessages;
        DeadLetterBytes = deadLetterBytes;
        OldestPendingAge = oldestPendingAge;
        OldestProcessingAge = oldestProcessingAge;
        OldestDeadLetterAge = oldestDeadLetterAge;
        SuccessfulPublishCount = successfulPublishCount;
        FailedPublishAttemptCount = failedPublishAttemptCount;
        EnqueuedMessageCount = enqueuedMessageCount;
        DeadLetteredMessageCount = deadLetteredMessageCount;
        ExpiredDeadLetterCount = expiredDeadLetterCount;
        LastFailureUtc = lastFailureUtc;
        LastFailureMessage = lastFailureMessage;
    }

    /// <summary>
    /// 获取失效消息主题。
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// 获取是否启用 Redis outbox。
    /// </summary>
    public bool OutboxEnabled { get; }

    /// <summary>
    /// 获取 pending 队列中的消息数。
    /// </summary>
    public long PendingMessages { get; }

    /// <summary>
    /// 获取 processing 队列中的消息数。
    /// </summary>
    public long ProcessingMessages { get; }

    /// <summary>
    /// 获取 dead-letter 消息数。
    /// </summary>
    public long DeadLetterMessages { get; }

    /// <summary>
    /// 获取 dead-letter payload 的 UTF-8 字节总数。
    /// </summary>
    /// <remarks>未启用字节预算时为 <see langword="null"/>。</remarks>
    public long? DeadLetterBytes { get; }

    /// <summary>
    /// 获取 pending 中最早消息的年龄。
    /// </summary>
    /// <remarks>无法解析旧消息时间时为 <see langword="null"/>。</remarks>
    public TimeSpan? OldestPendingAge { get; }

    /// <summary>
    /// 获取 processing 中最早消息的年龄。
    /// </summary>
    /// <remarks>无法解析旧消息时间时为 <see langword="null"/>。</remarks>
    public TimeSpan? OldestProcessingAge { get; }

    /// <summary>
    /// 获取 dead-letter 中最早消息的年龄。
    /// </summary>
    /// <remarks>无记录或无法读取时间时为 <see langword="null"/>。</remarks>
    public TimeSpan? OldestDeadLetterAge { get; }

    /// <summary>
    /// 获取当前进程观察到的成功发布次数。
    /// </summary>
    public long SuccessfulPublishCount { get; }

    /// <summary>
    /// 获取当前进程观察到的失败发布次数。
    /// </summary>
    public long FailedPublishAttemptCount { get; }

    /// <summary>
    /// 获取当前进程成功写入 outbox 的消息数。
    /// </summary>
    public long EnqueuedMessageCount { get; }

    /// <summary>
    /// 获取当前进程成功隔离到 dead-letter 的消息数。
    /// </summary>
    public long DeadLetteredMessageCount { get; }

    /// <summary>
    /// 获取当前进程成功清理的 dead-letter 消息数。
    /// </summary>
    public long ExpiredDeadLetterCount { get; }

    /// <summary>
    /// 获取当前进程最近一次失败时间。
    /// </summary>
    /// <remarks>时间采用 UTC。</remarks>
    public DateTimeOffset? LastFailureUtc { get; }

    /// <summary>
    /// 获取当前进程最近一次失败信息。
    /// </summary>
    public string LastFailureMessage { get; }
}

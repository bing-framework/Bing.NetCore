using EasyCaching.Core.Bus;

namespace Bing.EasyCaching;

/// <summary>
/// 定义 Hybrid 失效消息待处理队列与 dead-letter 的持久化操作。
/// </summary>
internal interface IHybridInvalidationOutbox : IDisposable
{
    /// <summary>
    /// 尝试将失效消息加入待处理队列。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">待入队的失效消息。</param>
    /// <returns>成功入队时返回 <see langword="true"/>；容量不足时返回 <see langword="false"/>。</returns>
    bool TryEnqueue(string topic, EasyCachingMessage message);

    /// <summary>
    /// 异步尝试将失效消息加入待处理队列。
    /// </summary>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">待入队的失效消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功入队时返回 <see langword="true"/>；容量不足时返回 <see langword="false"/>。</returns>
    Task<bool> TryEnqueueAsync(string topic, EasyCachingMessage message,
        CancellationToken cancellationToken);

    /// <summary>
    /// 尝试从待处理队列领取一条失效消息。
    /// </summary>
    /// <param name="item">领取的消息；未领取成功时为默认值。</param>
    /// <returns>领取成功时返回 <see langword="true"/>；队列为空时返回 <see langword="false"/>。</returns>
    bool TryTake(out HybridInvalidationOutboxItem item);

    /// <summary>
    /// 异步尝试从待处理队列领取一条失效消息。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>领取的消息；队列为空时返回 <see langword="null"/>。</returns>
    Task<HybridInvalidationOutboxItem> TryTakeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 尝试将处理失败的消息移入 dead-letter 队列。
    /// </summary>
    /// <param name="item">待移入 dead-letter 的消息。</param>
    /// <returns>成功移入时返回 <see langword="true"/>；容量不足或消息已不存在时返回 <see langword="false"/>。</returns>
    bool TryDeadLetter(HybridInvalidationOutboxItem item);

    /// <summary>
    /// 异步尝试将处理失败的消息移入 dead-letter 队列。
    /// </summary>
    /// <param name="item">待移入 dead-letter 的消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>成功移入时返回 <see langword="true"/>；容量不足或消息已不存在时返回 <see langword="false"/>。</returns>
    Task<bool> TryDeadLetterAsync(HybridInvalidationOutboxItem item,
        CancellationToken cancellationToken);

    /// <summary>
    /// 清理截止时间之前的过期 dead-letter 消息。
    /// </summary>
    /// <param name="cutoffUtc">清理截止时间。</param>
    /// <param name="maximumMessages">本次最多清理的消息数。</param>
    /// <returns>删除的消息数。</returns>
    long CleanupExpiredDeadLetters(DateTimeOffset cutoffUtc, int maximumMessages);

    /// <summary>
    /// 异步清理截止时间之前的过期 dead-letter 消息。
    /// </summary>
    /// <param name="cutoffUtc">清理截止时间。</param>
    /// <param name="maximumMessages">本次最多清理的消息数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>删除的消息数。</returns>
    Task<long> CleanupExpiredDeadLettersAsync(DateTimeOffset cutoffUtc, int maximumMessages,
        CancellationToken cancellationToken);

    /// <summary>
    /// 获取待处理、处理中和 dead-letter 队列状态。
    /// </summary>
    /// <returns>当前 outbox 状态。</returns>
    HybridInvalidationOutboxStatus GetStatus();

    /// <summary>
    /// 异步获取待处理、处理中和 dead-letter 队列状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前 outbox 状态。</returns>
    Task<HybridInvalidationOutboxStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 确认并移除已成功发布的 outbox 消息。
    /// </summary>
    /// <param name="item">已处理的 outbox 消息。</param>
    void Acknowledge(HybridInvalidationOutboxItem item);

    /// <summary>
    /// 异步确认并移除已成功发布的 outbox 消息。
    /// </summary>
    /// <param name="item">已处理的 outbox 消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AcknowledgeAsync(HybridInvalidationOutboxItem item, CancellationToken cancellationToken);
}

/// <summary>
/// 表示 outbox 中待处理或正在重试的失效消息。
/// </summary>
internal sealed class HybridInvalidationOutboxItem
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationOutboxItem"/> 类型的实例。
    /// </summary>
    /// <remarks>首次投递次数设为 1。</remarks>
    /// <param name="payload">消息序列化载荷。</param>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">反序列化后的失效消息。</param>
    internal HybridInvalidationOutboxItem(string payload, string topic, EasyCachingMessage message)
        : this(payload, topic, message, 1)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationOutboxItem"/> 类型的实例。
    /// </summary>
    /// <param name="payload">消息序列化载荷。</param>
    /// <param name="topic">消息主题。</param>
    /// <param name="message">反序列化后的失效消息。</param>
    /// <param name="deliveryAttempt">当前投递次数。</param>
    internal HybridInvalidationOutboxItem(string payload, string topic, EasyCachingMessage message,
        long deliveryAttempt)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Topic = topic ?? throw new ArgumentNullException(nameof(topic));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        DeliveryAttempt = deliveryAttempt;
    }

    /// <summary>
    /// 获取消息序列化载荷。
    /// </summary>
    internal string Payload { get; }

    /// <summary>
    /// 获取消息主题。
    /// </summary>
    internal string Topic { get; }

    /// <summary>
    /// 获取失效消息对象。
    /// </summary>
    internal EasyCachingMessage Message { get; }

    /// <summary>
    /// 获取当前投递次数。
    /// </summary>
    internal long DeliveryAttempt { get; }
}

/// <summary>
/// 保存 Hybrid 失效 outbox 各队列的数量和最早消息时间。
/// </summary>
internal sealed class HybridInvalidationOutboxStatus
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationOutboxStatus"/> 类型的实例。
    /// </summary>
    /// <remarks>未提供 dead-letter 载荷字节数时，该值保持未指定。</remarks>
    /// <param name="pendingMessages">待处理消息数。</param>
    /// <param name="processingMessages">处理中消息数。</param>
    /// <param name="deadLetterMessages">dead-letter 消息数。</param>
    /// <param name="oldestPendingEnqueuedAtUtc">最早待处理消息的入队时间。</param>
    /// <param name="oldestProcessingEnqueuedAtUtc">最早处理中消息的入队时间。</param>
    /// <param name="oldestDeadLetteredAtUtc">最早 dead-letter 消息的记录时间。</param>
    internal HybridInvalidationOutboxStatus(long pendingMessages, long processingMessages,
        long deadLetterMessages, DateTimeOffset? oldestPendingEnqueuedAtUtc,
        DateTimeOffset? oldestProcessingEnqueuedAtUtc,
        DateTimeOffset? oldestDeadLetteredAtUtc)
        : this(pendingMessages, processingMessages, deadLetterMessages,
            oldestPendingEnqueuedAtUtc, oldestProcessingEnqueuedAtUtc,
            oldestDeadLetteredAtUtc, null)
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationOutboxStatus"/> 类型的实例。
    /// </summary>
    /// <param name="pendingMessages">待处理消息数。</param>
    /// <param name="processingMessages">处理中消息数。</param>
    /// <param name="deadLetterMessages">dead-letter 消息数。</param>
    /// <param name="oldestPendingEnqueuedAtUtc">最早待处理消息的入队时间。</param>
    /// <param name="oldestProcessingEnqueuedAtUtc">最早处理中消息的入队时间。</param>
    /// <param name="oldestDeadLetteredAtUtc">最早 dead-letter 消息的记录时间。</param>
    /// <param name="deadLetterBytes">dead-letter 载荷总字节数。</param>
    internal HybridInvalidationOutboxStatus(long pendingMessages, long processingMessages,
        long deadLetterMessages, DateTimeOffset? oldestPendingEnqueuedAtUtc,
        DateTimeOffset? oldestProcessingEnqueuedAtUtc,
        DateTimeOffset? oldestDeadLetteredAtUtc, long? deadLetterBytes)
    {
        PendingMessages = pendingMessages;
        ProcessingMessages = processingMessages;
        DeadLetterMessages = deadLetterMessages;
        OldestPendingEnqueuedAtUtc = oldestPendingEnqueuedAtUtc;
        OldestProcessingEnqueuedAtUtc = oldestProcessingEnqueuedAtUtc;
        OldestDeadLetteredAtUtc = oldestDeadLetteredAtUtc;
        DeadLetterBytes = deadLetterBytes;
    }

    /// <summary>
    /// 获取待处理消息数。
    /// </summary>
    internal long PendingMessages { get; }

    /// <summary>
    /// 获取处理中消息数。
    /// </summary>
    internal long ProcessingMessages { get; }

    /// <summary>
    /// 获取 dead-letter 消息数。
    /// </summary>
    internal long DeadLetterMessages { get; }

    /// <summary>
    /// 获取最早待处理消息的入队时间。
    /// </summary>
    internal DateTimeOffset? OldestPendingEnqueuedAtUtc { get; }

    /// <summary>
    /// 获取最早处理中消息的入队时间。
    /// </summary>
    internal DateTimeOffset? OldestProcessingEnqueuedAtUtc { get; }

    /// <summary>
    /// 获取最早 dead-letter 消息的记录时间。
    /// </summary>
    internal DateTimeOffset? OldestDeadLetteredAtUtc { get; }

    /// <summary>
    /// 获取 dead-letter 序列化载荷总字节数。
    /// </summary>
    /// <remarks>未采集时返回 <see langword="null"/>。</remarks>
    internal long? DeadLetterBytes { get; }
}

namespace Bing.EasyCaching;

/// <summary>
/// 指定 Hybrid dead-letter 管理操作。
/// </summary>
public enum HybridInvalidationDeadLetterAction
{
    /// <summary>
    /// 查看 dead-letter 条目。
    /// </summary>
    View = 0,

    /// <summary>
    /// 重放 dead-letter 条目。
    /// </summary>
    Replay = 1,

    /// <summary>
    /// 确认删除 dead-letter 条目。
    /// </summary>
    ConfirmDelete = 2
}

/// <summary>
/// 提供 Hybrid Redis outbox dead-letter 的受控人工操作。
/// </summary>
/// <remarks>
/// 操作只适用于启用 Redis outbox 的 Hybrid 实例。实现不会替调用方执行身份认证；每个操作
/// 都要求宿主提供授权器和审计接收器。条目的 <see cref="HybridInvalidationDeadLetterEntry.Handle"/>
/// 只应作为后续操作的 opaque handle，不应写入普通应用日志。
/// </remarks>
public interface IHybridInvalidationDeadLetterAdmin
{
    /// <summary>
    /// 读取一批 dead-letter 条目。
    /// </summary>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="maximumMessages">最多读取的消息数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按进入 dead-letter 时间排序的记录。</returns>
    Task<IReadOnlyList<HybridInvalidationDeadLetterEntry>> GetDeadLettersAsync(
        string actor, int maximumMessages = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 dead-letter 条目重新放回待投递队列。
    /// </summary>
    /// <param name="handle">条目的不透明句柄。</param>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>条目存在并已重新放入队列时返回 <see langword="true"/>。</returns>
    Task<bool> ReplayAsync(string handle, string actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用确认令牌删除 dead-letter 条目。
    /// </summary>
    /// <param name="handle">条目的不透明句柄。</param>
    /// <param name="confirmationToken">删除确认令牌。</param>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>条目存在并已删除时返回 <see langword="true"/>。</returns>
    Task<bool> ConfirmDeleteAsync(string handle, string confirmationToken, string actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将 dead-letter 条目重新放回待投递队列。
    /// </summary>
    /// <param name="entry">dead-letter 条目。</param>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>条目存在并已重新放入队列时返回 <see langword="true"/>。</returns>
    Task<bool> ReplayAsync(HybridInvalidationDeadLetterEntry entry, string actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用确认令牌删除 dead-letter 条目。
    /// </summary>
    /// <param name="entry">dead-letter 条目。</param>
    /// <param name="confirmationToken">删除确认令牌。</param>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>条目存在并已删除时返回 <see langword="true"/>。</returns>
    Task<bool> ConfirmDeleteAsync(HybridInvalidationDeadLetterEntry entry,
        string confirmationToken, string actor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示 Redis outbox 中的一条 dead-letter 条目。
/// </summary>
public sealed class HybridInvalidationDeadLetterEntry
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDeadLetterEntry"/> 类型的实例。
    /// </summary>
    /// <param name="topic">失效消息主题。</param>
    /// <param name="messageId">失效消息 ID。</param>
    /// <param name="cacheKeys">消息中的缓存键快照。</param>
    /// <param name="isPrefix">是否为前缀失效消息。</param>
    /// <param name="isPattern">是否为模式失效消息。</param>
    /// <param name="deadLetteredAtUtc">进入 dead-letter 的时间。</param>
    /// <param name="payloadBytes">原始 JSON envelope 的 UTF-8 字节数。</param>
    /// <param name="malformed">payload 是否无法解析。</param>
    /// <param name="handle">供后续管理操作使用的不透明句柄。</param>
    /// <param name="confirmationToken">删除确认令牌。</param>
    internal HybridInvalidationDeadLetterEntry(string topic, string messageId,
        IReadOnlyList<string> cacheKeys, bool isPrefix, bool isPattern,
        DateTimeOffset? deadLetteredAtUtc, long payloadBytes, bool malformed,
        string handle, string confirmationToken)
    {
        Topic = topic;
        MessageId = messageId;
        CacheKeys = cacheKeys ?? Array.Empty<string>();
        IsPrefix = isPrefix;
        IsPattern = isPattern;
        DeadLetteredAtUtc = deadLetteredAtUtc;
        PayloadBytes = payloadBytes;
        Malformed = malformed;
        Handle = handle;
        ConfirmationToken = confirmationToken;
    }

    /// <summary>
    /// 获取失效消息主题。
    /// </summary>
    /// <remarks>payload 损坏且无法解析时为空字符串。</remarks>
    public string Topic { get; }

    /// <summary>
    /// 获取失效消息 ID。
    /// </summary>
    /// <remarks>payload 损坏且无法解析时为空字符串。</remarks>
    public string MessageId { get; }

    /// <summary>
    /// 获取消息中的缓存键快照。
    /// </summary>
    /// <remarks>payload 损坏时为空集合。</remarks>
    public IReadOnlyList<string> CacheKeys { get; }

    /// <summary>
    /// 获取是否按前缀失效。
    /// </summary>
    public bool IsPrefix { get; }

    /// <summary>
    /// 获取是否按模式失效。
    /// </summary>
    public bool IsPattern { get; }

    /// <summary>
    /// 获取进入 dead-letter 的时间。
    /// </summary>
    /// <remarks>Redis 分数无效时为 <see langword="null"/>。</remarks>
    public DateTimeOffset? DeadLetteredAtUtc { get; }

    /// <summary>
    /// 获取原始 JSON envelope 的 UTF-8 字节数。
    /// </summary>
    public long PayloadBytes { get; }

    /// <summary>
    /// 获取 payload 是否无法解析。
    /// </summary>
    /// <remarks>损坏条目仍可确认删除。</remarks>
    public bool Malformed { get; }

    /// <summary>
    /// 获取后续管理操作使用的不透明句柄。
    /// </summary>
    public string Handle { get; }

    /// <summary>
    /// 获取删除确认令牌。
    /// </summary>
    public string ConfirmationToken { get; }

}

/// <summary>
/// 向授权器描述一次 dead-letter 管理请求。
/// </summary>
public sealed class HybridInvalidationDeadLetterAuthorizationRequest
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDeadLetterAuthorizationRequest"/> 类型的实例。
    /// </summary>
    /// <param name="action">请求的管理操作。</param>
    /// <param name="topic">目标消息主题。</param>
    /// <param name="actor">发起操作的主体。</param>
    /// <param name="messageId">目标消息 ID。</param>
    /// <param name="requestedCount">请求读取的最大条数。</param>
    /// <param name="handle">目标条目的不透明句柄。</param>
    internal HybridInvalidationDeadLetterAuthorizationRequest(
        HybridInvalidationDeadLetterAction action, string topic, string actor,
        string messageId, int requestedCount, string handle = null)
    {
        Action = action;
        Topic = topic;
        Actor = actor;
        MessageId = messageId;
        RequestedCount = requestedCount;
        Handle = handle;
    }

    /// <summary>
    /// 获取请求的管理操作。
    /// </summary>
    public HybridInvalidationDeadLetterAction Action { get; }

    /// <summary>
    /// 获取目标消息主题。
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// 获取由调用方提供的审计主体。
    /// </summary>
    public string Actor { get; }

    /// <summary>
    /// 获取目标消息 ID。
    /// </summary>
    /// <remarks>查看整批时为空字符串。</remarks>
    public string MessageId { get; }

    /// <summary>
    /// 获取请求读取的最大条数。
    /// </summary>
    /// <remarks>单条操作的值为 1。</remarks>
    public int RequestedCount { get; }

    /// <summary>
    /// 获取目标条目的不透明句柄。
    /// </summary>
    /// <remarks>授权发生在读取 Redis payload 之前。</remarks>
    public string Handle { get; }
}

/// <summary>
/// 为 dead-letter 管理操作提供授权决策。
/// </summary>
public interface IHybridInvalidationDeadLetterAuthorizer
{
    /// <summary>
    /// 授权管理请求或拒绝该请求。
    /// </summary>
    /// <param name="request">待授权的管理请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AuthorizeAsync(HybridInvalidationDeadLetterAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 表示一条 dead-letter 管理审计记录。
/// </summary>
public sealed class HybridInvalidationDeadLetterAuditEntry
{
    /// <summary>
    /// 初始化一个 <see cref="HybridInvalidationDeadLetterAuditEntry"/> 类型的实例。
    /// </summary>
    /// <param name="action">执行的管理操作。</param>
    /// <param name="topic">目标消息主题。</param>
    /// <param name="actor">操作主体。</param>
    /// <param name="messageId">目标消息 ID。</param>
    /// <param name="succeeded">Redis 状态变更是否成功。</param>
    /// <param name="affectedCount">实际影响的条目数。</param>
    /// <param name="occurredAtUtc">审计记录时间。</param>
    /// <param name="failureReason">失败原因。</param>
    internal HybridInvalidationDeadLetterAuditEntry(
        HybridInvalidationDeadLetterAction action, string topic, string actor,
        string messageId, bool succeeded, int affectedCount,
        DateTimeOffset occurredAtUtc, string failureReason)
    {
        Action = action;
        Topic = topic;
        Actor = actor;
        MessageId = messageId;
        Succeeded = succeeded;
        AffectedCount = affectedCount;
        OccurredAtUtc = occurredAtUtc;
        FailureReason = failureReason;
    }

    /// <summary>
    /// 获取执行的管理操作。
    /// </summary>
    public HybridInvalidationDeadLetterAction Action { get; }

    /// <summary>
    /// 获取目标消息主题。
    /// </summary>
    public string Topic { get; }

    /// <summary>
    /// 获取操作主体。
    /// </summary>
    public string Actor { get; }

    /// <summary>
    /// 获取目标消息 ID。
    /// </summary>
    /// <remarks>批量查看时为空字符串。</remarks>
    public string MessageId { get; }

    /// <summary>
    /// 获取 Redis 状态变更是否成功。
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// 获取本次操作影响的条目数。
    /// </summary>
    public int AffectedCount { get; }

    /// <summary>
    /// 获取审计记录时间。
    /// </summary>
    /// <remarks>时间采用 UTC。</remarks>
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// 获取失败原因。
    /// </summary>
    /// <remarks>操作成功时为空字符串。</remarks>
    public string FailureReason { get; }
}

/// <summary>
/// 接收 dead-letter 管理审计记录。
/// </summary>
public interface IHybridInvalidationDeadLetterAuditSink
{
    /// <summary>
    /// 记录一次管理操作。
    /// </summary>
    /// <param name="entry">管理审计记录。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task RecordAsync(HybridInvalidationDeadLetterAuditEntry entry,
        CancellationToken cancellationToken = default);
}

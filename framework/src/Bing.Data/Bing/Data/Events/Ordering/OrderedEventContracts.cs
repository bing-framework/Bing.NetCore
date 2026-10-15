using System.Data.Common;
using Bing.Data.Transaction;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 有序事件存储使用的数据库语法。
/// </summary>
public enum OrderedEventDatabase
{
    /// <summary>
    /// MySQL 数据库。
    /// </summary>
    MySql,
    /// <summary>
    /// SQLite 数据库。
    /// </summary>
    Sqlite
}

/// <summary>
/// 单次有序处理的结果。
/// </summary>
public enum OrderedEventProcessingResult
{
    /// <summary>
    /// 当前没有待处理事件。
    /// </summary>
    Empty,
    /// <summary>
    /// 前序事件缺失。
    /// </summary>
    Gap,
    /// <summary>
    /// 前序事件失败，需要显式重试。
    /// </summary>
    Failed,
    /// <summary>
    /// 已执行业务并推进事务内游标。
    /// </summary>
    Processed
}

/// <summary>
/// 订阅方完整可见的实体业务流。
/// </summary>
public sealed class OrderedEventStream
{
    /// <summary>
    /// 初始化一个 <see cref="OrderedEventStream" /> 类型的实例。
    /// </summary>
    /// <param name="tenantId">租户标识；无租户时为空。</param>
    /// <param name="stream">稳定的业务流名称。</param>
    /// <param name="entityKey">实体标识；删除重建时应包含新的流代次。</param>
    public OrderedEventStream(string tenantId, string stream, string entityKey)
    {
        TenantId = tenantId ?? string.Empty;
        Stream = string.IsNullOrWhiteSpace(stream) ? throw new ArgumentException("业务流不能为空。", nameof(stream)) : stream;
        EntityKey = string.IsNullOrWhiteSpace(entityKey) ? throw new ArgumentException("实体标识不能为空。", nameof(entityKey)) : entityKey;
        if (TenantId.Length > 200 || Stream.Length > 200 || EntityKey.Length > 200)
            throw new ArgumentException("业务流的各标识不能超过 200 个字符。");
    }

    /// <summary>
    /// 租户标识。
    /// </summary>
    public string TenantId { get; }
    /// <summary>
    /// 业务流名称。
    /// </summary>
    public string Stream { get; }
    /// <summary>
    /// 实体标识。
    /// </summary>
    public string EntityKey { get; }
}

/// <summary>
/// 持久化的有序事件内容。
/// </summary>
public sealed class OrderedEventMessage
{
    /// <summary>
    /// 初始化一个 <see cref="OrderedEventMessage" /> 类型的实例。
    /// </summary>
    public OrderedEventMessage(string eventId, long sequence, string payload, string contractVersion = null)
    {
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 128)
            throw new ArgumentException("事件标识不能为空且不能超过 128 个字符。", nameof(eventId));
        if (sequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (contractVersion?.Length > 100)
            throw new ArgumentException("契约版本不能超过 100 个字符。", nameof(contractVersion));
        EventId = eventId;
        Sequence = sequence;
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        ContractVersion = contractVersion ?? string.Empty;
    }

    /// <summary>
    /// 事件标识。
    /// </summary>
    public string EventId { get; }
    /// <summary>
    /// 连续序号。
    /// </summary>
    public long Sequence { get; }
    /// <summary>
    /// 序列化事件内容。
    /// </summary>
    public string Payload { get; }
    /// <summary>
    /// 契约版本。
    /// </summary>
    public string ContractVersion { get; }
}

/// <summary>
/// 业务流的持久化处理状态。
/// </summary>
public sealed class OrderedEventStreamState
{
    /// <summary>
    /// 初始化一个 <see cref="OrderedEventStreamState" /> 类型的实例。
    /// </summary>
    public OrderedEventStreamState(OrderedEventStream stream, long lastSequence, long? firstPendingSequence,
        long pendingCount, bool failed, int attempts, string lastError)
    {
        Stream = stream;
        LastSequence = lastSequence;
        FirstPendingSequence = firstPendingSequence;
        PendingCount = pendingCount;
        Failed = failed;
        Attempts = attempts;
        LastError = lastError;
    }

    /// <summary>
    /// 业务流。
    /// </summary>
    public OrderedEventStream Stream { get; }
    /// <summary>
    /// 已处理序号。
    /// </summary>
    public long LastSequence { get; }
    /// <summary>
    /// 首个待处理序号。
    /// </summary>
    public long? FirstPendingSequence { get; }
    /// <summary>
    /// 待处理数量。
    /// </summary>
    public long PendingCount { get; }
    /// <summary>
    /// 首个待处理事件是否失败。
    /// </summary>
    public bool Failed { get; }
    /// <summary>
    /// 已记录失败次数。
    /// </summary>
    public int Attempts { get; }
    /// <summary>
    /// 最近的安全错误标识。
    /// </summary>
    public string LastError { get; }
}

/// <summary>
/// 管理一次有序事件数据库事务。
/// </summary>
/// <remarks>业务处理器必须使用此事务，不能提前提交。事务操作不能与提交或释放并发；重复释放等待同一次清理，资源所有者只释放本次事务。</remarks>
public interface IOrderedEventTransaction : IAsyncDisposable
{
    /// <summary>
    /// 当前数据库事务。
    /// </summary>
    DbTransaction Transaction { get; }
    /// <summary>
    /// 提交事务。
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// 回滚事务。
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供有序事务的提交结果与业务保存点回滚清理。
/// </summary>
/// <remarks>仅由需要管理事务外状态的适配器实现，不改变现有事务实现契约。</remarks>
public interface IOrderedEventTransactionState : IOrderedEventTransaction
{
    /// <summary>
    /// 激活调用方执行上下文中的事务适配状态。
    /// </summary>
    /// <remarks>调用方在等待开启或重置完成后同步调用；异步方法内部修改 AsyncLocal 不会回流到调用方。</remarks>
    void ActivateAmbient();

    /// <summary>
    /// 获取当前能够确认的物理事务结果。
    /// </summary>
    TransactionOutcome Outcome { get; }

    /// <summary>
    /// 清理已回滚业务对应的事务外状态。
    /// </summary>
    /// <param name="cancellationToken">清理取消令牌。</param>
    /// <remarks>数据库保存点已经回滚；清理失败必须回滚整笔事务，不能继续提交失败标记。</remarks>
    Task ResetBusinessStateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 创建当前作用域的有序事件事务。
/// </summary>
public interface IOrderedEventTransactionFactory
{
    /// <summary>
    /// 开启事务。
    /// </summary>
    Task<IOrderedEventTransaction> BeginAsync(CancellationToken cancellationToken = default);
}

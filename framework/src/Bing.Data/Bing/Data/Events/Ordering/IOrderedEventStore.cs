using System.Data.Common;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 在调用方事务内维护有序事件及处理游标。
/// </summary>
/// <remarks>所有写入与业务修改必须一起提交；该存储不创建、提交或释放传入的事务。</remarks>
public interface IOrderedEventStore
{
    /// <summary>
    /// 分配业务流的下一连续序号。
    /// </summary>
    Task<long> AllocateSequenceAsync(DbTransaction transaction, OrderedEventStream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 持久化接收事件。
    /// </summary>
    /// <remarks>完全相同的重复事件幂等接受；同一 ID 或序号对应不同内容时拒绝。</remarks>
    Task EnqueueAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        OrderedEventMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行下一事件并推进游标。
    /// </summary>
    /// <remarks>回调失败时调用方必须回滚；完成记录和业务修改在同一事务中。</remarks>
    Task<OrderedEventProcessingResult> ProcessNextAsync(DbTransaction transaction, string consumer,
        OrderedEventStream stream, Func<OrderedEventMessage, DbTransaction, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 记录下一事件失败并阻止后续处理。
    /// </summary>
    /// <remarks>确认业务修改已回滚后记录；协调器使用保存点保留游标锁，避免另开事务的竞争窗口。错误标识不得包含正文或敏感参数。</remarks>
    Task RecordFailureAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        long failedSequence, string errorCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 允许失败的前序事件重试。
    /// </summary>
    Task RetryAsync(DbTransaction transaction, string consumer, OrderedEventStream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取业务流处理状态。
    /// </summary>
    /// <remarks>只读取调用方事务可见的快照，不创建未知流或显式请求写锁；未知流返回空状态。隔离级别和观测超时由调用方决定。</remarks>
    Task<OrderedEventStreamState> GetStateAsync(DbTransaction transaction, string consumer,
        OrderedEventStream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页读取仍有待处理事件的业务流。
    /// </summary>
    Task<IReadOnlyList<OrderedEventStream>> GetPendingStreamsAsync(DbTransaction transaction, string consumer,
        int limit, string afterStreamKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取业务流的稳定存储键。
    /// </summary>
    string GetStreamKey(OrderedEventStream stream);
}

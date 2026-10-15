using System.Data.Common;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 提供已完成有序事件的受控留存清理能力。
/// </summary>
/// <remarks>
/// 该契约是可选扩展，不会改变 <see cref="IOrderedEventStore"/> 的必需成员。
/// 实现只能清理已经完成的收件记录，不能按时间删除待处理或失败记录。
/// 内置数据库存储会先写入已处理历史表，再删除收件箱，以保留事件 ID 的重复校验；历史表不会由该方法自动清理，
/// 如需在重放窗口结束后清理历史，请使用独立的 <see cref="IOrderedEventStoreArchiveRetention"/> 契约。
/// </remarks>
public interface IOrderedEventStoreRetention
{
    /// <summary>
    /// 删除指定消费者在截止时间以前已经完成的收件记录。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cutoffUtc">留存截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次删除的记录数。</returns>
    /// <remarks>
    /// 调用方必须在独立事务中按批次执行，并保留游标、审计和业务幂等记录。
    /// 未完成记录不会被该方法删除。
    /// </remarks>
    Task<int> DeleteProcessedBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default);
}

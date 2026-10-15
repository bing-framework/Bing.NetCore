using System.Data.Common;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 提供已处理有序事件历史记录的受控清理能力。
/// </summary>
/// <remarks>
/// 该契约与 <see cref="IOrderedEventStoreRetention"/> 分离，避免清理收件箱时意外删除重复事件依据。
/// 调用方必须先确定允许重放的时间范围，并在独立事务中按批次执行；框架不会默认调度此操作。
/// </remarks>
public interface IOrderedEventStoreArchiveRetention
{
    /// <summary>
    /// 删除指定消费者在截止时间以前的已处理事件历史记录。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cutoffUtc">历史记录截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次删除的历史记录数。</returns>
    /// <remarks>
    /// 删除后，事件 ID 超出保留窗口可能再次被接收；外部业务副作用仍必须使用自己的幂等记录。
    /// 待处理和失败收件记录不属于该历史表，不会由此方法删除。
    /// </remarks>
    Task<int> DeleteArchivedBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default);
}

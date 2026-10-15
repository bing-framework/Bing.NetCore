using System.Data.Common;

namespace Bing.Events.Consumption;

/// <summary>
/// 提供经过授权的事件消费收据清理能力。
/// </summary>
/// <remarks>
/// 该服务保留调用方事务边界，适合由受信运维任务在确认重放窗口后显式调用。
/// </remarks>
public interface IEventConsumptionRetentionService
{
    /// <summary>
    /// 授权并删除指定消费者在截止时间以前的消费收据。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">稳定的逻辑消费者名称。</param>
    /// <param name="cutoffUtc">留存截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次实际删除的记录数。</returns>
    Task<int> DeleteBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default);
}

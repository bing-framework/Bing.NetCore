using System.Data.Common;

namespace Bing.Events.Consumption;

/// <summary>
/// 提供事件消费收据的受控留存清理能力。
/// </summary>
/// <remarks>
/// 该契约是可选扩展，不会改变 <see cref="IEventConsumptionStore"/> 的登记行为。
/// 清理只删除指定消费者在留存截止时间以前的收据，不会自动启动后台任务，也不会替调用方决定重放窗口、授权范围或归档策略。
/// </remarks>
public interface IEventConsumptionRetentionStore
{
    /// <summary>
    /// 删除指定消费者在截止时间以前的消费收据。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">稳定的逻辑消费者名称。</param>
    /// <param name="cutoffUtc">留存截止时间，必须为 UTC。</param>
    /// <param name="batchSize">本次最多删除的记录数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>本次删除的记录数。</returns>
    /// <remarks>
    /// 调用方必须先确认业务允许的重放期限已经结束，并保留仍需用于幂等校验的记录。
    /// </remarks>
    Task<int> DeleteBeforeAsync(DbTransaction transaction, string consumer, DateTime cutoffUtc,
        int batchSize, CancellationToken cancellationToken = default);
}

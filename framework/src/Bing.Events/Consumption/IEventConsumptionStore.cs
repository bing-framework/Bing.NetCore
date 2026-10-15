using System.Data.Common;

namespace Bing.Events.Consumption;

/// <summary>
/// 在业务事务内记录事件的逻辑消费者身份。
/// </summary>
public interface IEventConsumptionStore
{
    /// <summary>
    /// 尝试在当前事务中登记首次消费。
    /// </summary>
    /// <param name="transaction">与业务写入共享的数据库事务。</param>
    /// <param name="consumer">稳定的逻辑消费者名称。</param>
    /// <param name="eventId">事件唯一标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>首次登记成功返回 <c>true</c>，重复投递返回 <c>false</c>。</returns>
    Task<bool> TryRecordAsync(DbTransaction transaction, string consumer, string eventId,
        CancellationToken cancellationToken = default);
}

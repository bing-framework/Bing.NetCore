using System.Data.Common;

namespace Bing.Data.Events.Ordering;

/// <summary>
/// 提供有序事件存储的可选统计查询。
/// </summary>
/// <remarks>
/// 该契约不属于 <see cref="IOrderedEventStore"/> 的必需成员，旧的自定义存储可以继续只实现主契约。
/// </remarks>
public interface IOrderedEventStoreStatistics
{
    /// <summary>
    /// 获取逻辑消费者的全局待处理事件数。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前事务可见的待处理事件数。</returns>
    Task<long> GetPendingEventCountAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供有序事件存储的全局待处理业务流数量查询。
/// </summary>
/// <remarks>
/// 该契约是可选扩展，旧的自定义存储不实现时消费者继续使用最近一页流数作为兼容回退值。
/// </remarks>
public interface IOrderedEventStorePendingStreamStatistics
{
    /// <summary>
    /// 获取逻辑消费者的全局待处理业务流数量。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>当前事务可见的待处理业务流数量。</returns>
    Task<long> GetPendingStreamCountAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 提供有序事件存储的最老积压时间查询。
/// </summary>
/// <remarks>
/// 该契约是可选扩展，旧的自定义存储不实现时消费者仍可运行，但不会报告最老事件年龄。
/// </remarks>
public interface IOrderedEventStorePendingAgeStatistics
{
    /// <summary>
    /// 获取逻辑消费者最老待处理事件的入箱时间。
    /// </summary>
    /// <param name="transaction">调用方拥有的数据库事务。</param>
    /// <param name="consumer">逻辑消费者名称。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最老事件的 UTC 入箱时间；没有积压时返回 <see langword="null"/>。</returns>
    Task<DateTime?> GetOldestPendingEventUtcAsync(DbTransaction transaction, string consumer,
        CancellationToken cancellationToken = default);
}

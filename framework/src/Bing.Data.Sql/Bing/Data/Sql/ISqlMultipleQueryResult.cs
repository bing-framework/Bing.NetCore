namespace Bing.Data.Sql;

/// <summary>
/// 多结果集命令的顺序读取结果。
/// </summary>
/// <remarks>
/// 结果集必须按 SQL 语句顺序读取，并且必须释放该对象以归还连接、事务和执行租约。
/// </remarks>
public interface ISqlMultipleQueryResult : IDisposable
{
    /// <summary>
    /// 读取当前结果集的动态行集合。
    /// </summary>
    /// <returns>已物化的当前结果集。</returns>
    List<dynamic> Read();

    /// <summary>
    /// 读取当前结果集的实体集合。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <returns>已物化的当前结果集。</returns>
    List<TEntity> Read<TEntity>();

    /// <summary>
    /// 异步读取当前结果集的动态行集合。
    /// </summary>
    /// <remarks>该重载无法在开始读取前响应取消，请改用接收 <see cref="CancellationToken"/> 的重载。</remarks>
    /// <returns>表示最终结果集的异步操作。</returns>
    [Obsolete("请使用接收 CancellationToken 的 ReadAsync 重载")]
    Task<List<dynamic>> ReadAsync();

    /// <summary>
    /// 异步读取当前结果集的实体集合。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <remarks>该重载无法在开始读取前响应取消，请改用接收 <see cref="CancellationToken"/> 的重载。</remarks>
    /// <returns>表示最终结果集的异步操作。</returns>
    [Obsolete("请使用接收 CancellationToken 的 ReadAsync 重载")]
    Task<List<TEntity>> ReadAsync<TEntity>();

    /// <summary>
    /// 异步读取当前结果集的动态行集合。
    /// </summary>
    /// <param name="cancellationToken">开始读取当前结果集前使用的取消令牌。</param>
    /// <returns>表示最终结果集的异步操作。</returns>
    Task<List<dynamic>> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步读取当前结果集的实体集合。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="cancellationToken">开始读取当前结果集前使用的取消令牌。</param>
    /// <returns>表示最终结果集的异步操作。</returns>
    Task<List<TEntity>> ReadAsync<TEntity>(CancellationToken cancellationToken = default);
}
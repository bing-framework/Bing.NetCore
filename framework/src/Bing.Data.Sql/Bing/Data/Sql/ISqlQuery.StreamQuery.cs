namespace Bing.Data.Sql;

// ISqlQuery - StreamQuery
public partial interface ISqlQuery
{
    /// <summary>
    /// 以非缓冲方式流式获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>实体流</returns>
    IEnumerable<TEntity> StreamQuery<TEntity>(int? timeout = null);

    /// <summary>
    /// 以非缓冲方式异步流式获取实体集合
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体异步流</returns>
    IAsyncEnumerable<TEntity> StreamQueryAsync<TEntity>(int? timeout = null,
        CancellationToken cancellationToken = default);
}
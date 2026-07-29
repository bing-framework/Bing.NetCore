using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql;

/// <summary>
/// 实体删除执行器。
/// </summary>
public interface ISqlDeleteExecutor
{
    /// <summary>
    /// 删除单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待删除实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>受影响行数。</returns>
    int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步删除单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待删除实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终受影响行数的异步操作。</returns>
    Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量删除实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="options">批量删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>实际受影响行数。</returns>
    int DeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量删除实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除实体集合。</param>
    /// <param name="options">批量删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终实际受影响行数的异步操作。</returns>
    Task<int> DeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;
}
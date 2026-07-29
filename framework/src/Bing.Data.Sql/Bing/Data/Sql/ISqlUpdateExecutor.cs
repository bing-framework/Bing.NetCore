using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql;

/// <summary>
/// 实体更新执行器。
/// </summary>
public interface ISqlUpdateExecutor
{
    /// <summary>
    /// 更新单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待更新实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>受影响行数。</returns>
    int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步更新单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待更新实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终受影响行数的异步操作。</returns>
    Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量更新实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">批量更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>实际受影响行数。</returns>
    int UpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量更新实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新实体集合。</param>
    /// <param name="options">批量更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终实际受影响行数的异步操作。</returns>
    Task<int> UpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;
}
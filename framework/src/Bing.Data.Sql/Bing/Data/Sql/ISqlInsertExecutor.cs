using Bing.Data.Sql.Mutations;
using Bing.Data.Sql.Builders.Mutations.Batching;

namespace Bing.Data.Sql;

/// <summary>
/// 实体插入执行器。
/// </summary>
public interface ISqlInsertExecutor
{
    /// <summary>
    /// 插入单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待插入实体。</param>
    /// <param name="options">插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>受影响行数。</returns>
    int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步插入单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待插入实体。</param>
    /// <param name="options">插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终受影响行数的异步操作。</returns>
    Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量插入实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>实际受影响行数。</returns>
    int InsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量插入实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终实际受影响行数的异步操作。</returns>
    Task<int> InsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;
}
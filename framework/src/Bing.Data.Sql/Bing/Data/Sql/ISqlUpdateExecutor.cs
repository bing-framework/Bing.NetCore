using Bing.Data.Sql.Mutations;

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
    /// <returns>表示最终受影响行数的异步操作。</returns>
    Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class;
}
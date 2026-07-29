using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 基于实体映射生成单实体写入命令的 Builder。
/// </summary>
public interface ISqlMutationBuilder
{
    /// <summary>
    /// 生成插入实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待插入实体。</param>
    /// <param name="options">插入选项。</param>
    /// <returns>可执行 SQL 命令快照。</returns>
    SqlMutationCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成更新实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待更新实体。</param>
    /// <param name="options">更新选项。</param>
    /// <returns>可执行 SQL 命令快照。</returns>
    SqlMutationCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成删除实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待删除实体。</param>
    /// <param name="options">删除选项。</param>
    /// <returns>可执行 SQL 命令快照。</returns>
    SqlMutationCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;
}
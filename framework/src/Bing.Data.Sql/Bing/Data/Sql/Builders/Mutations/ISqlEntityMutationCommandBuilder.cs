using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 基于实体映射生成单实体写入命令的 Builder。
/// </summary>
public interface ISqlEntityMutationCommandBuilder
{
    /// <summary>
    /// 生成插入实体的 SQL 命令。
    /// </summary>
    SqlMutationCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成更新实体的 SQL 命令。
    /// </summary>
    SqlMutationCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成删除实体的 SQL 命令。
    /// </summary>
    SqlMutationCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;
}

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
    /// <typeparam name="TEntity">待插入实体类型。</typeparam>
    /// <param name="entity">包含待写入属性值的实体。</param>
    /// <param name="options">可选的 Insert 列筛选和执行配置。</param>
    /// <returns>带参数和元数据的可执行 Insert 命令。</returns>
    SqlWriteCommand Insert<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成更新实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="entity">包含待写入、主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Update 列筛选、原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Update 命令。</returns>
    SqlWriteCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成删除实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Delete 原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Delete 命令。</returns>
    SqlWriteCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;
}

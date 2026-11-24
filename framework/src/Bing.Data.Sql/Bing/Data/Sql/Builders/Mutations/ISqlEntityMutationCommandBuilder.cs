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

    /// <summary>生成插入并返回整条实体记录的命令。</summary>
    /// <typeparam name="TEntity">待插入实体类型。</typeparam>
    /// <param name="entity">包含待写入属性值的实体。</param>
    /// <param name="options">可选的插入列筛选。</param>
    /// <returns>交给 <see cref="ISqlExecutor.ExecuteReturning{TResult}"/> 读取的独立命令。</returns>
    /// <remarks>返回所有映射列，使用 CLR 属性名作为别名；不自动回填输入实体的生成键。</remarks>
    SqlWriteCommand InsertReturning<TEntity>(TEntity entity, SqlInsertOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成更新实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="entity">包含待写入、主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Update 列筛选、原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Update 命令。</returns>
    SqlWriteCommand Update<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;

    /// <summary>生成更新并返回更新后整条实体记录的命令。</summary>
    /// <typeparam name="TEntity">待更新实体类型。</typeparam>
    /// <param name="entity">包含待写入、主键和并发属性值的实体。</param>
    /// <param name="options">可选的更新列筛选、原始值和并发配置。</param>
    /// <returns>包含整行返回列与并发语义的独立命令。</returns>
    SqlWriteCommand UpdateReturning<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成删除实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Delete 原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Delete 命令。</returns>
    SqlWriteCommand Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;

    /// <summary>生成删除并返回整条实体记录的命令。</summary>
    /// <typeparam name="TEntity">待删除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="options">可选的原始值和并发配置。</param>
    /// <returns>物理删除前的记录；逻辑删除实体返回更新后的记录。</returns>
    /// <remarks>返回结果由现有 Executor 读取，事务仍由执行上下文决定。</remarks>
    SqlWriteCommand DeleteReturning<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成物理清除实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待物理清除实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Delete 原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Delete 命令。</returns>
    SqlWriteCommand Purge<TEntity>(TEntity entity, SqlDeleteOptions options = null) where TEntity : class;

    /// <summary>
    /// 生成恢复逻辑删除实体的 SQL 命令。
    /// </summary>
    /// <typeparam name="TEntity">待恢复实体类型。</typeparam>
    /// <param name="entity">包含主键和并发属性值的实体。</param>
    /// <param name="options">可选的 Update 原始值和并发配置。</param>
    /// <returns>带参数和并发校验信息的可执行 Update 命令。</returns>
    SqlWriteCommand Restore<TEntity>(TEntity entity, SqlUpdateOptions options = null) where TEntity : class;
}

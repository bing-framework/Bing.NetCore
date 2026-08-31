using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Mutations.Batching;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Builder、连接和事务状态，不支持并发复用；每个并发操作必须由 Factory 创建独立实例。
/// </remarks>
public interface ISqlExecutor : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// 插入单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待插入的实体。</param>
    /// <param name="options">插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>插入操作影响的行数。</returns>
    int Insert<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步插入单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待插入的实体。</param>
    /// <param name="options">插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示插入操作的异步操作，结果为影响的行数。</returns>
    Task<int> InsertAsync<TEntity>(TEntity entity, SqlInsertOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量插入实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入的实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>批量插入操作影响的行数。</returns>
    int InsertBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量插入实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待插入的实体集合。</param>
    /// <param name="options">批量插入选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示批量插入操作的异步操作，结果为影响的行数。</returns>
    Task<int> InsertBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchInsertOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 更新单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待更新的实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>更新操作影响的行数。</returns>
    int Update<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步更新单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待更新的实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示更新操作的异步操作，结果为影响的行数。</returns>
    Task<int> UpdateAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量更新实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新的实体集合。</param>
    /// <param name="options">批量更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>批量更新操作影响的行数。</returns>
    int UpdateBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量更新实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待更新的实体集合。</param>
    /// <param name="options">批量更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示批量更新操作的异步操作，结果为影响的行数。</returns>
    Task<int> UpdateBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchUpdateOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 删除单个实体；逻辑删除实体将更新删除状态。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待删除的实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>删除操作影响的行数。</returns>
    int Delete<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步删除单个实体；逻辑删除实体将更新删除状态。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待删除的实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示删除操作的异步操作，结果为影响的行数。</returns>
    Task<int> DeleteAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 物理清除单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待物理删除的实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>物理删除操作影响的行数。</returns>
    int Purge<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步物理清除单个实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待物理删除的实体。</param>
    /// <param name="options">删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示物理删除操作的异步操作，结果为影响的行数。</returns>
    Task<int> PurgeAsync<TEntity>(TEntity entity, SqlDeleteOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 恢复单个逻辑删除实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待恢复的实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>恢复操作影响的行数。</returns>
    int Restore<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null) where TEntity : class;

    /// <summary>
    /// 异步恢复单个逻辑删除实体。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entity">待恢复的实体。</param>
    /// <param name="options">更新选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示恢复操作的异步操作，结果为影响的行数。</returns>
    Task<int> RestoreAsync<TEntity>(TEntity entity, SqlUpdateOptions options = null, int? timeout = null,
        CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 批量删除实体；逻辑删除实体将逐条更新删除状态。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除的实体集合。</param>
    /// <param name="options">批量删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>批量删除操作影响的行数。</returns>
    int DeleteBatch<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null, int? timeout = null)
        where TEntity : class;

    /// <summary>
    /// 异步批量删除实体；逻辑删除实体将逐条更新删除状态。
    /// </summary>
    /// <typeparam name="TEntity">实体类型。</typeparam>
    /// <param name="entities">待删除的实体集合。</param>
    /// <param name="options">批量删除选项。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示批量删除操作的异步操作，结果为影响的行数。</returns>
    Task<int> DeleteBatchAsync<TEntity>(IEnumerable<TEntity> entities, SqlBatchDeleteOptions options = null,
        int? timeout = null, CancellationToken cancellationToken = default) where TEntity : class;

    /// <summary>
    /// 创建当前执行上下文专属的独立 Mutation SQL 生成器。
    /// </summary>
    /// <returns>不与 Root Executor 或其他操作共享可变状态的 SQL 生成器。</returns>
    ISqlBuilder CreateWriteBuilder();

    /// <summary>
    /// 执行独立写入命令表示的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="command">已冻结的写入命令。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <returns>操作影响的行数。</returns>
    int ExecuteWrite(SqlWriteCommand command, int? timeout = null);

    /// <summary>
    /// 异步执行独立写入命令表示的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="command">已冻结的写入命令。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示操作影响行数的异步操作。</returns>
    Task<int> ExecuteWriteAsync(SqlWriteCommand command, int? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行指定 SQL 文本。
    /// 原生 SQL 不会自动应用结构化全局过滤器。
    /// </summary>
    /// <param name="sql">执行的 SQL 语句。</param>
    /// <param name="param">SQL 参数。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <returns>操作影响的行数。</returns>
    int ExecuteSql(string sql, object param = null, int? timeout = null);

    /// <summary>
    /// 异步执行指定 SQL 文本。原生 SQL 不会自动应用结构化全局过滤器。
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作影响的行数</returns>
    Task<int> ExecuteSqlAsync(string sql, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <returns>包含受影响行数及本次输出参数访问器的过程执行结果。</returns>
    SqlProcedureResult<int> ExecuteProcedure(string procedure, object param = null, int? timeout = null);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表示包含受影响行数及本次输出参数访问器的过程执行结果的异步操作。</returns>
    Task<SqlProcedureResult<int>> ExecuteProcedureAsync(string procedure, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行带 Returning 或 Output 的 Mutation，并同步物化返回行。
    /// </summary>
    /// <typeparam name="TResult">返回行映射类型。</typeparam>
    /// <param name="command">已冻结的带 Returning 写入命令。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>返回行集合。</returns>
    List<TResult> ExecuteReturning<TResult>(SqlWriteCommand command, int? timeout = null);

    /// <summary>
    /// 异步执行带 Returning 或 Output 的 Mutation，并物化返回行。
    /// </summary>
    /// <typeparam name="TResult">返回行映射类型。</typeparam>
    /// <param name="command">已冻结的带 Returning 写入命令。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示返回行集合的异步操作。</returns>
    Task<List<TResult>> ExecuteReturningAsync<TResult>(SqlWriteCommand command, int? timeout = null,
        CancellationToken cancellationToken = default);

}

using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Builder、连接和事务状态，不支持并发复用；每个并发操作必须由 Factory 创建独立实例。
/// </remarks>
public interface ISqlExecutor : IDisposable, ISqlInsertExecutor, ISqlUpdateExecutor, ISqlDeleteExecutor
{
    /// <summary>
    /// 配置执行器选项。
    /// </summary>
    /// <param name="configAction">配置操作。</param>
    void Config(Action<SqlOptions> configAction);

    /// <summary>
    /// 获取当前 Mutation SQL 生成器。
    /// </summary>
    /// <returns>可用于构建 Insert、Update 或 Delete 命令的 SQL 生成器。</returns>
    ISqlBuilder GetBuilder();

    /// <summary>
    /// 执行统一 Builder 生成的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="builder">SQL Builder。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <returns>操作影响的行数。</returns>
    int Execute(ISqlBuilder builder, int? timeout = null);

    /// <summary>
    /// 执行统一 Builder 生成的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="builder">SQL Builder。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>操作影响的行数。</returns>
    Task<int> ExecuteAsync(ISqlBuilder builder, int? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行指定的SQL语句
    /// </summary>
    /// <param name="sql">执行的SQL语句</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间。单位：秒</param>
    /// <returns>操作影响的行数</returns>
    int ExecuteSql(string sql, object param = null, int? timeout = null);

    /// <summary>
    /// 执行指定的SQL语句
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
    /// 执行带 Returning 或 Output 的 Mutation，并物化返回行。
    /// </summary>
    /// <typeparam name="TResult">返回行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示返回行集合的异步操作。</returns>
    Task<List<TResult>> ExecuteReturningQueryAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default);
}

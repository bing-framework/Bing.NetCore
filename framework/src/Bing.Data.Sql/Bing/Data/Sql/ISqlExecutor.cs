using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Mutations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Builder、连接和事务状态，不支持并发复用；每个并发操作必须由 Factory 创建独立实例。
/// </remarks>
public interface ISqlExecutor : IDisposable, IAsyncDisposable, ISqlInsertExecutor, ISqlUpdateExecutor, ISqlDeleteExecutor
{
    /// <summary>
    /// 创建当前执行上下文专属的独立 Mutation SQL 生成器。
    /// </summary>
    /// <returns>不与 Root Executor 或其他操作共享可变状态的 SQL 生成器。</returns>
    ISqlBuilder CreateBuilder();

    /// <summary>
    /// 执行独立写入命令表示的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="command">已冻结的写入命令。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <returns>操作影响的行数。</returns>
    int ExecuteMutation(SqlWriteCommand command, int? timeout = null);

    /// <summary>
    /// 异步执行独立写入命令表示的 Insert、Update 或 Delete 操作。
    /// </summary>
    /// <param name="command">已冻结的写入命令。</param>
    /// <param name="timeout">执行超时时间。单位：秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示操作影响行数的异步操作。</returns>
    Task<int> ExecuteMutationAsync(SqlWriteCommand command, int? timeout = null,
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

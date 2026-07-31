using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// Sql执行对象
/// </summary>
/// <remarks>
/// 实例包含可变的 Builder、连接和事务状态，不支持并发复用；每个并发操作必须由 Factory 创建独立实例。
/// </remarks>
public interface ISqlExecutor : ISqlQuery, ISqlInsertExecutor, ISqlUpdateExecutor, ISqlDeleteExecutor
{
    /// <summary>
    /// 最近一次执行的输出参数访问器
    /// </summary>
    ISqlOutputParameterAccessor OutputParameters { get; }

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
    /// <returns>受影响行数</returns>
    int ExecuteProcedure(string procedure, object param = null, int? timeout = null);

    /// <summary>
    /// 执行存储过程增删改操作
    /// </summary>
    /// <param name="procedure">存储过程</param>
    /// <param name="param">SQL参数</param>
    /// <param name="timeout">执行超时时间，单位：秒</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响行数</returns>
    Task<int> ExecuteProcedureAsync(string procedure, object param = null, int? timeout = null,
        CancellationToken cancellationToken = default);
}

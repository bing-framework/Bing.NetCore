using Bing.Data.Sql.Builders.Multiple;

namespace Bing.Data.Sql;

/// <summary>
/// 单次数据库往返执行多个查询并顺序读取结果集的执行器。
/// </summary>
public interface ISqlMultipleQueryExecutor : IDisposable
{
    /// <summary>
    /// 创建独立的多结果集批处理命令 Builder。
    /// </summary>
    /// <returns>批处理命令 Builder。</returns>
    ISqlMultipleQueryBatchBuilder CreateBatch();

    /// <summary>
    /// 执行多结果集命令。
    /// </summary>
    /// <param name="command">待执行命令。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>按顺序读取结果集的对象。</returns>
    ISqlMultipleQueryResult Execute(SqlMultipleQueryCommand command, int? timeout = null);

    /// <summary>
    /// 异步执行多结果集命令。
    /// </summary>
    /// <param name="command">待执行命令。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终读取对象的异步操作。</returns>
    Task<ISqlMultipleQueryResult> ExecuteAsync(SqlMultipleQueryCommand command, int? timeout = null,
        CancellationToken cancellationToken = default);
}
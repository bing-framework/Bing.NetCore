namespace Bing.Data.Sql;

/// <summary>
/// 执行独立 SQL 查询计划的分页与流式运行时契约。
/// </summary>
public partial interface ISqlQueryPlanExecutor
{
    /// <summary>
    /// 同步执行分页查询并返回总数与当前页数据。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的结构化查询计划。</param>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页信息和结果行的集合。</returns>
    PagerList<TResult> ToPage<TResult>(SqlQueryPlan plan, IPager pager, int? timeout);

    /// <summary>
    /// 以同步流方式执行查询计划。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    IEnumerable<TResult> AsEnumerable<TResult>(SqlQueryPlan plan, int? timeout);

    /// <summary>
    /// 异步执行分页查询并返回总数与当前页数据。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的结构化查询计划。</param>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含分页信息和结果行集合的异步操作。</returns>
    Task<PagerList<TResult>> ToPageAsync<TResult>(SqlQueryPlan plan, IPager pager, int? timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// 以异步流方式执行查询计划。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="plan">待执行的查询计划。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(SqlQueryPlan plan, int? timeout,
        CancellationToken cancellationToken);
}
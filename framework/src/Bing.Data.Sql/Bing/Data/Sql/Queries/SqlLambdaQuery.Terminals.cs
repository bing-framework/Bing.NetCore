namespace Bing.Data.Sql;

/// <summary>
/// 结构化 Lambda 查询的 SQL 渲染、克隆和终结方法。
/// </summary>
public partial class SqlLambdaQuery
{
    /// <summary>
    /// 生成当前查询的 SQL 文本。
    /// </summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    public string ToSql() => _core.ToSql();

    /// <summary>
    /// 克隆当前查询描述为独立的 Draft 查询。
    /// </summary>
    /// <returns>拥有独立 Builder、参数和查询上下文的查询描述。</returns>
    public SqlLambdaQuery Clone() => new(_core.Clone());

    /// <summary>
    /// 同步执行当前查询并完整物化指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    public List<TResult> ToList<TResult>(int? timeout = null) => _core.ToList<TResult>(timeout);

    /// <summary>
    /// 查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    public TResult ToEntity<TResult>(int? timeout = null) => _core.ToEntity<TResult>(timeout);

    /// <summary>
    /// 同步执行当前查询并获取指定结果类型的第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    public TResult First<TResult>(int? timeout = null) => _core.First<TResult>(timeout);

    /// <summary>
    /// 同步执行当前查询并获取指定结果类型的第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果或默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _core.FirstOrDefault<TResult>(timeout);

    /// <summary>
    /// 同步执行当前查询并获取指定结果类型的唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    public TResult Single<TResult>(int? timeout = null) => _core.Single<TResult>(timeout);

    /// <summary>
    /// 同步执行当前查询并获取指定结果类型的首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值。</returns>
    public TResult Scalar<TResult>(int? timeout = null) => _core.Scalar<TResult>(timeout);

    /// <summary>
    /// 同步执行当前查询并返回指定结果类型的分页结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>分页结果。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _core.ToPage<TResult>(pager, timeout);

    /// <summary>
    /// 以同步流方式执行当前查询并映射为指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _core.AsEnumerable<TResult>(timeout);

    /// <summary>
    /// 异步执行当前查询并完整物化指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行或默认值的异步操作。</returns>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToEntityAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前查询并获取指定结果类型的第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _core.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前查询并获取指定结果类型的第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果或默认值的异步操作。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前查询并获取指定结果类型的唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _core.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前查询并获取指定结果类型的首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前查询并返回指定结果类型的分页结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示分页结果的异步操作。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>
    /// 以异步流方式执行当前查询并映射为指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.AsAsyncEnumerable<TResult>(timeout, cancellationToken);
}

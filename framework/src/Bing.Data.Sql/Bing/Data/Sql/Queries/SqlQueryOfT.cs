using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 指定结果类型的独立 Fluent SQL 查询描述。
/// </summary>
/// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
/// <remarks>
/// 实例保留独立 Builder，并在执行时使用指定结果类型映射每一行。
/// </remarks>
public class SqlQuery<TResult> : ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 复用执行链和独立 Builder 的内部查询描述。
    /// </summary>
    private readonly SqlQuery _query;

    /// <summary>
    /// 使用独立 SQL Builder 初始化指定结果类型的查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    internal SqlQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) => _query = new SqlQuery(executor, builder);

    /// <summary>
    /// 在保留当前独立 Builder 和执行器的前提下切换结果映射类型。
    /// </summary>
    /// <typeparam name="TNextResult">后续执行时用于映射结果行的类型。</typeparam>
    /// <returns>使用同一查询状态但具有新结果类型的查询描述。</returns>
    internal SqlQuery<TNextResult> WithResult<TNextResult>() => _query.WithResult<TNextResult>();

    /// <summary>
    /// 获取内部查询核心，供 raw Fluent 子类型实现多映射终结方法。
    /// </summary>
    internal SqlQuery QueryCore => _query;

    /// <summary>
    /// 同步执行当前 Fluent 查询并完整物化结果集。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    public List<TResult> ToList(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    public TResult First(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行或默认值。</returns>
    public TResult FirstOrDefault(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    public TResult Single(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    public TResult SingleOrDefault(int? timeout = null) => _query.SingleOrDefault<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值；无结果时返回默认值。</returns>
    public TResult Scalar(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并返回指定页的数据和总行数。
    /// </summary>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页信息和结果行的集合。</returns>
    public PagerList<TResult> ToPage(IPager pager = null, int? timeout = null) => _query.ToPage<TResult>(pager, timeout);

    /// <summary>
    /// 以同步流方式执行当前 Fluent 查询。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>
    /// 异步执行当前 Fluent 查询并完整物化结果集。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    public Task<TResult> FirstAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行或默认值的异步操作。</returns>
    public Task<TResult> FirstOrDefaultAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    public Task<TResult> SingleAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一一行或默认值的异步操作。</returns>
    public Task<TResult> SingleOrDefaultAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.SingleOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作；无结果时返回默认值。</returns>
    public Task<TResult> ScalarAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并返回指定页的数据和总行数。
    /// </summary>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含分页信息和结果行集合的异步操作。</returns>
    public Task<PagerList<TResult>> ToPageAsync(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>
    /// 以异步流方式执行当前 Fluent 查询。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 获取当前查询专属的 SQL Builder。
    /// </summary>
    internal ISqlBuilder GetBuilder() => _query.GetBuilder();

    /// <summary>
    /// 设置原始 Fluent 多映射分段列。
    /// </summary>
    internal void SetSplitOn(string splitOn) => _query.SplitOn(splitOn);

    /// <summary>
    /// 获取当前查询描述使用的内部计划执行器。
    /// </summary>
    /// <remarks>
    /// 仅供同程序集的强类型查询描述创建共享 Builder 的后继对象。
    /// </remarks>
    internal ISqlQueryPlanExecutor Executor => _query.Executor;

    /// <summary>
    /// 生成当前查询的 SQL 文本。
    /// </summary>
    public string ToSql() => _query.ToSql();

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => _query.GetBuilder();

}

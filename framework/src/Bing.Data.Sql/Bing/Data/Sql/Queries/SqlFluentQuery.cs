using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// 结果类型由终结方法选择的 Fluent SQL 查询描述。
/// </summary>
public sealed class SqlFluentQuery : ISqlQueryOperation, ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 承载当前查询描述和执行逻辑的查询对象。
    /// </summary>
    private readonly SqlQuery _query;

    /// <summary>
    /// 初始化一个 <see cref="SqlFluentQuery"/> 类型的实例。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">当前查询使用的 SQL 生成器。</param>
    internal SqlFluentQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        _query = new SqlQuery(executor, builder);

    /// <summary>设置 Dapper 多映射使用的分段列名称。</summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前查询描述。</returns>
    public SqlFluentQuery SplitOn(string splitOn)
    {
        _query.SplitOn(splitOn);
        return this;
    }

    /// <summary>生成当前查询的 SQL 文本。</summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    public string ToSql() => _query.ToSql();

    /// <summary>同步执行并返回全部结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果列表。</returns>
    public List<TResult> ToList<TResult>(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>同步执行两段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行三段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行四段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行五段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行六段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TSixth">第六段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行七段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TSixth">第六段查询结果类型。</typeparam>
    /// <typeparam name="TSeventh">第七段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行并返回至多一行结果。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的实体；没有结果时返回类型默认值。</returns>
    public TResult ToEntity<TResult>(int? timeout = null) => _query.ToEntity<TResult>(timeout);

    /// <summary>同步执行并返回第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行。</returns>
    public TResult First<TResult>(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>同步执行并返回第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行；没有结果时返回类型默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>同步执行并返回唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的唯一结果。</returns>
    public TResult Single<TResult>(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>同步执行并返回标量值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询得到的标量值。</returns>
    public TResult Scalar<TResult>(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>同步执行并返回分页结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页数据和分页信息的结果。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _query.ToPage<TResult>(pager, timeout);

    /// <summary>同步流式读取结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>按需读取查询结果的可枚举序列。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>异步执行并返回全部结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行两段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行三段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行四段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行五段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行六段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TSixth">第六段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行七段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TFourth">第四段查询结果类型。</typeparam>
    /// <typeparam name="TFifth">第五段查询结果类型。</typeparam>
    /// <typeparam name="TSixth">第六段查询结果类型。</typeparam>
    /// <typeparam name="TSeventh">第七段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含多映射查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行并返回至多一行结果。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询实体的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToEntityAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) =>
        _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询唯一结果的异步任务。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回标量值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询标量值的异步任务。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回分页结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页数据和分页信息的异步任务。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) =>
        _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>异步流式读取结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按需读取查询结果的异步可枚举序列。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    /// <summary>获取当前查询专属的 Builder。</summary>
    /// <returns>当前查询使用的 SQL 生成器。</returns>
    internal ISqlBuilder GetBuilder() => _query.GetBuilder();

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => _query.GetBuilder();

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => _query.Touch();
}
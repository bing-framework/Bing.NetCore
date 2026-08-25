using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Operations;

namespace Bing.Data.Sql;

/// <summary>
/// 结果类型由终结方法选择的 Fluent SQL 查询描述。
/// </summary>
public sealed class SqlFluentQuery : ISqlQueryOperation, ISqlQueryBuilderAccessor
{
    private readonly SqlQuery _query;

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
    public string ToSql() => _query.ToSql();

    /// <summary>同步执行并返回全部结果。</summary>
    public List<TResult> ToList<TResult>(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>同步执行两段 Dapper 多映射查询。</summary>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行三段 Dapper 多映射查询。</summary>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行四段 Dapper 多映射查询。</summary>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) => _query.ToList(map, timeout);

    /// <summary>同步执行五段 Dapper 多映射查询。</summary>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行六段 Dapper 多映射查询。</summary>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行七段 Dapper 多映射查询。</summary>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null) =>
        _query.ToList(map, timeout);

    /// <summary>同步执行并返回至多一行结果。</summary>
    public TResult ToEntity<TResult>(int? timeout = null) => _query.ToEntity<TResult>(timeout);

    /// <summary>同步执行并返回第一行。</summary>
    public TResult First<TResult>(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>同步执行并返回第一行或默认值。</summary>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>同步执行并返回唯一一行。</summary>
    public TResult Single<TResult>(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>同步执行并返回标量值。</summary>
    public TResult Scalar<TResult>(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>同步执行并返回分页结果。</summary>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _query.ToPage<TResult>(pager, timeout);

    /// <summary>同步流式读取结果。</summary>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>异步执行并返回全部结果。</summary>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行两段 Dapper 多映射查询。</summary>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行三段 Dapper 多映射查询。</summary>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行四段 Dapper 多映射查询。</summary>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行五段 Dapper 多映射查询。</summary>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行六段 Dapper 多映射查询。</summary>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行七段 Dapper 多映射查询。</summary>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行并返回至多一行结果。</summary>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToEntityAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回第一行。</summary>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回第一行或默认值。</summary>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) =>
        _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回唯一一行。</summary>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回标量值。</summary>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行并返回分页结果。</summary>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) =>
        _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>异步流式读取结果。</summary>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    /// <summary>获取当前查询专属的 Builder。</summary>
    internal ISqlBuilder GetBuilder() => _query.GetBuilder();

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => _query.GetBuilder();
}
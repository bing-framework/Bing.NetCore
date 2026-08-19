namespace Bing.Data.Sql;

/// <summary>
/// 低层 Fluent SQL 查询描述。
/// </summary>
/// <typeparam name="TResult">默认结果映射类型。</typeparam>
/// <remarks>
/// 该类型保留原始字符串 Builder 扩展和 Dapper 多对象映射能力；类型化 Lambda 查询不继承此类型。
/// </remarks>
public sealed class SqlFluentQuery<TResult> : SqlQuery<TResult>, Bing.Data.Sql.Builders.Operations.ISqlQueryOperation
{
    /// <summary>
    /// 使用独立 SQL Builder 初始化低层 Fluent 查询描述。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">独立 SQL Builder。</param>
    internal SqlFluentQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder)
        : base(executor, builder)
    {
    }

    /// <summary>
    /// 设置 Dapper 多映射使用的分段列名称。
    /// </summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前原始 Fluent 查询描述。</returns>
    public SqlFluentQuery<TResult> SplitOn(string splitOn)
    {
        SetSplitOn(splitOn);
        return this;
    }

    /// <summary>同步执行两段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond>(Func<TFirst, TSecond, TResult> map, int? timeout = null) =>
        QueryCore.ToList(map, timeout);

    /// <summary>同步执行三段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond, TThird>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => QueryCore.ToList(map, timeout);

    /// <summary>同步执行四段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) =>
        QueryCore.ToList(map, timeout);

    /// <summary>同步执行五段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        QueryCore.ToList(map, timeout);

    /// <summary>同步执行六段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        QueryCore.ToList(map, timeout);

    /// <summary>同步执行七段 Dapper 多映射查询。</summary>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout = null) => QueryCore.ToList(map, timeout);

    /// <summary>异步执行两段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        QueryCore.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行三段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird>(
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => QueryCore.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行四段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => QueryCore.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行五段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => QueryCore.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行六段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => QueryCore.ToListAsync(map, timeout, cancellationToken);

    /// <summary>异步执行七段 Dapper 多映射查询。</summary>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        QueryCore.ToListAsync(map, timeout, cancellationToken);
}
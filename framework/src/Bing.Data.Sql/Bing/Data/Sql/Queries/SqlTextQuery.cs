using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 原生 SQL 文本查询描述，结果类型由终结方法选择。
/// </summary>
public sealed class SqlTextQuery
{
    private readonly ISqlQueryPlanExecutor _executor;
    private readonly object _parameters;
    private string _splitOn = "Id";

    internal SqlTextQuery(ISqlQueryPlanExecutor executor, string commandText, object parameters)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("SQL 文本不能为空。", nameof(commandText));
        CommandText = commandText;
        _parameters = SqlQueryPlan.SnapshotParameters(parameters);
    }

    /// <summary>
    /// 获取要执行的 SQL 文本。
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// 获取查询参数快照。
    /// </summary>
    public object Parameters => SqlQueryPlan.SnapshotParameters(_parameters);

    /// <summary>
    /// 设置多映射分段列。
    /// </summary>
    public SqlTextQuery SplitOn(string splitOn)
    {
        if (string.IsNullOrWhiteSpace(splitOn))
            throw new ArgumentException("多映射分段列不能为空。", nameof(splitOn));
        _splitOn = splitOn;
        return this;
    }

    /// <summary>
    /// 查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    public TResult ToEntity<TResult>(int? timeout = null) => _executor.SingleOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 查询全部结果。
    /// </summary>
    public List<TResult> ToList<TResult>(int? timeout = null) => _executor.ToList<TResult>(GetPlan(), timeout);

    /// <summary>同步执行两段 Dapper 多映射查询。</summary>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行三段 Dapper 多映射查询。</summary>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行四段 Dapper 多映射查询。</summary>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行五段 Dapper 多映射查询。</summary>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行六段 Dapper 多映射查询。</summary>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行七段 Dapper 多映射查询。</summary>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 查询原生 SQL 的一页结果，并在总数未知时自动执行安全计数查询。
    /// </summary>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _executor.ToPage<TResult>(GetPlan(), pager, timeout);

    /// <summary>
    /// 获取第一行或默认值。
    /// </summary>
    public TResult FirstOrDefault<TResult>(int? timeout = null) =>
        _executor.FirstOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取第一行，零行抛出异常。
    /// </summary>
    public TResult First<TResult>(int? timeout = null) => _executor.First<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取唯一一行，零行或多行抛出异常。
    /// </summary>
    public TResult Single<TResult>(int? timeout = null) => _executor.Single<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取首行首列值。
    /// </summary>
    public TResult Scalar<TResult>(int? timeout = null) => _executor.Scalar<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 以同步流方式读取结果。
    /// </summary>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) =>
        _executor.AsEnumerable<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleOrDefaultAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步查询全部结果。
    /// </summary>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>异步执行两段 Dapper 多映射查询。</summary>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>异步执行三段 Dapper 多映射查询。</summary>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(
        Func<TFirst, TSecond, TThird, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>异步执行四段 Dapper 多映射查询。</summary>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>异步执行五段 Dapper 多映射查询。</summary>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>异步执行六段 Dapper 多映射查询。</summary>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>异步执行七段 Dapper 多映射查询。</summary>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步查询原生 SQL 的一页结果，并在总数未知时自动执行安全计数查询。
    /// </summary>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToPageAsync<TResult>(GetPlan(), pager, timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取第一行或默认值。
    /// </summary>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstOrDefaultAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取第一行，零行抛出异常。
    /// </summary>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取唯一一行，零行或多行抛出异常。
    /// </summary>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.SingleAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取首行首列值。
    /// </summary>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ScalarAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 以异步流方式读取结果。
    /// </summary>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.AsAsyncEnumerable<TResult>(GetPlan(), timeout,
        cancellationToken);

    private SqlQueryPlan GetPlan() => SqlQueryPlan.Create(CommandText, _parameters, _splitOn, CommandType.Text);
}

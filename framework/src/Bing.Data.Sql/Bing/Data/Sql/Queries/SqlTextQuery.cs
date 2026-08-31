using System.Data;

namespace Bing.Data.Sql;

/// <summary>
/// 原生 SQL 文本查询描述，结果类型由终结方法选择。
/// </summary>
public sealed class SqlTextQuery
{
    /// <summary>
    /// 查询计划执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 原生 SQL 查询参数的初始快照。
    /// </summary>
    private readonly object _parameters;

    /// <summary>
    /// Dapper 多映射的分段列名称。
    /// </summary>
    private string _splitOn = "Id";

    /// <summary>
    /// 初始化一个 <see cref="SqlTextQuery"/> 类型的实例。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="commandText">要执行的 SQL 文本。</param>
    /// <param name="parameters">SQL 查询参数。</param>
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
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前 SQL 文本查询描述。</returns>
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
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的唯一结果；没有结果时返回类型默认值。</returns>
    public TResult ToEntity<TResult>(int? timeout = null) => _executor.SingleOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 查询全部结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果列表。</returns>
    public List<TResult> ToList<TResult>(int? timeout = null) => _executor.ToList<TResult>(GetPlan(), timeout);

    /// <summary>同步执行两段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>同步执行三段 Dapper 多映射查询。</summary>
    /// <typeparam name="TFirst">第一段查询结果类型。</typeparam>
    /// <typeparam name="TSecond">第二段查询结果类型。</typeparam>
    /// <typeparam name="TThird">第三段查询结果类型。</typeparam>
    /// <typeparam name="TResult">映射后的结果类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>多映射查询结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

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
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

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
        _executor.ToList(GetPlan(), map, timeout);

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
        _executor.ToList(GetPlan(), map, timeout);

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
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 查询原生 SQL 的一页结果，并在总数未知时自动执行安全计数查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 <see langword="null"/> 时使用默认分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页信息和结果行的集合。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _executor.ToPage<TResult>(GetPlan(), pager, timeout);

    /// <summary>
    /// 获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果；没有结果时返回类型默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null) =>
        _executor.FirstOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取第一行，零行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    public TResult First<TResult>(int? timeout = null) => _executor.First<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取唯一一行，零行或多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    public TResult Single<TResult>(int? timeout = null) => _executor.Single<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值；无结果时返回默认值。</returns>
    public TResult Scalar<TResult>(int? timeout = null) => _executor.Scalar<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 以同步流方式读取结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) =>
        _executor.AsEnumerable<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleOrDefaultAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步查询全部结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

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
        _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步查询原生 SQL 的一页结果，并在总数未知时自动执行安全计数查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 <see langword="null"/> 时使用默认分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页信息和结果行集合的异步任务。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToPageAsync<TResult>(GetPlan(), pager, timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含第一行结果的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstOrDefaultAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取第一行，零行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含第一行结果的异步任务。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取唯一一行，零行或多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含唯一结果行的异步任务。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.SingleAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含首行首列值的异步任务。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ScalarAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 以异步流方式读取结果。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.AsAsyncEnumerable<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 创建当前原生 SQL 文本的查询计划。
    /// </summary>
    /// <returns>按文本命令类型配置的查询计划。</returns>
    private SqlQueryPlan GetPlan() => SqlQueryPlan.Create(CommandText, _parameters, _splitOn, CommandType.Text);
}

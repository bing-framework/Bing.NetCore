namespace Bing.Data.Sql;

/// <summary>
/// 指定结果类型的原生 SQL 文本查询描述。
/// </summary>
/// <typeparam name="TResult">后续执行时用于映射结果行的类型。</typeparam>
/// <remarks>
/// 原生 SQL 文本保持原样，不执行 SQL 类型判定、参数名称重写或标识符转换。
/// </remarks>
public class SqlTextQuery<TResult>
{
    /// <summary>
    /// 执行当前文本查询计划的根查询内部执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// 获取当前描述绑定的内部计划执行器。
    /// </summary>
    private protected ISqlQueryPlanExecutor Executor => _executor;

    /// <summary>
    /// 使用根查询、SQL 文本和参数源初始化原生查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="commandText">要原样执行的 SQL 文本。</param>
    /// <param name="parameters">由后续参数绑定器处理的参数源。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="executor"/> 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当 <paramref name="commandText"/> 为空白时抛出。</exception>
    internal SqlTextQuery(ISqlQueryPlanExecutor executor, string commandText, object parameters)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        if (string.IsNullOrWhiteSpace(commandText))
            throw new ArgumentException("SQL 文本不能为空。", nameof(commandText));
        CommandText = commandText;
        Parameters = SqlQueryPlan.SnapshotParameters(parameters);
    }

    /// <summary>
    /// 获取要原样执行的 SQL 文本。
    /// </summary>
    public string CommandText { get; }

    /// <summary>
    /// 获取由参数绑定器处理的参数源。
    /// </summary>
    public object Parameters { get; }

    /// <summary>
    /// Dapper 多映射的分段列名称。
    /// </summary>
    private protected string SplitOnColumn { get; private set; } = "Id";

    /// <summary>
    /// 设置 Dapper 多映射使用的分段列名称。
    /// </summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前查询描述。</returns>
    public SqlTextQuery<TResult> SplitOn(string splitOn)
    {
        if (string.IsNullOrWhiteSpace(splitOn))
            throw new ArgumentException("多映射分段列不能为空。", nameof(splitOn));
        SplitOnColumn = splitOn;
        return this;
    }

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并完整物化结果集。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    public List<TResult> ToList(int? timeout = null) => Executor.ToList<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond>(Func<TFirst, TSecond, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth>(Func<TFirst, TSecond, TThird, TFourth, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询，并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    public TResult First(int? timeout = null) => _executor.First<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行或默认值。</returns>
    public TResult FirstOrDefault(int? timeout = null) => _executor.FirstOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    public TResult Single(int? timeout = null) => _executor.Single<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    public TResult SingleOrDefault(int? timeout = null) => _executor.SingleOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前原生 SQL 文本查询并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值；无结果时返回默认值。</returns>
    public TResult Scalar(int? timeout = null) => _executor.Scalar<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 以同步流方式执行当前原生 SQL 文本查询。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable(int? timeout = null) => _executor.AsEnumerable<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并完整物化结果集。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ToListAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond>(Func<TFirst, TSecond, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询，并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并获取第一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    public Task<TResult> FirstAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.FirstAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并获取第一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行或默认值的异步操作。</returns>
    public Task<TResult> FirstOrDefaultAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.FirstOrDefaultAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并获取唯一一行。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    public Task<TResult> SingleAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并获取唯一一行或默认值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一一行或默认值的异步操作。</returns>
    public Task<TResult> SingleOrDefaultAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleOrDefaultAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前原生 SQL 文本查询并获取首行首列值。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作；无结果时返回默认值。</returns>
    public Task<TResult> ScalarAsync(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ScalarAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 以异步流方式执行当前原生 SQL 文本查询。
    /// </summary>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.AsAsyncEnumerable<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 获取当前原生文本查询对应的内部执行计划。
    /// </summary>
    /// <returns>包含 SQL 文本和参数源的查询计划。</returns>
    private protected virtual SqlQueryPlan GetPlan() => SqlQueryPlan.Create(CommandText, Parameters, SplitOnColumn);
}
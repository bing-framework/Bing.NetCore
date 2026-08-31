using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Operations;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql;

/// <summary>
/// 独立 Fluent SQL 查询描述。
/// </summary>
/// <remarks>
/// 每个实例持有独立的 SQL Builder，可复用现有 Fluent 子句扩展而不污染创建它的根 <see cref="ISqlQuery"/>。
/// </remarks>
internal class SqlQuery : ISqlQueryOperation, ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 查询描述的生命周期阶段。
    /// </summary>
    private enum QueryPhase
    {
        /// <summary>可继续修改查询结构的草稿阶段。</summary>
        Draft,

        /// <summary>已生成执行计划且不能继续修改的冻结阶段。</summary>
        Frozen,

        /// <summary>正在执行查询的阶段。</summary>
        Executing,

        /// <summary>执行已完成的阶段。</summary>
        Completed
    }

    /// <summary>
    /// 承载当前查询子句、参数和方言状态的独立 SQL Builder。
    /// </summary>
    private readonly ISqlBuilder _builder;

    /// <summary>
    /// 执行当前查询计划的根查询内部执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// Dapper 多映射的分段列名称。
    /// </summary>
    private string _splitOn = "Id";

    /// <summary>
    /// 当前查询描述的生命周期阶段。
    /// </summary>
    private QueryPhase _phase = QueryPhase.Draft;

    /// <summary>
    /// 当前查询执行租约标记，用于禁止同一描述并发执行。
    /// </summary>
    private int _executionLease;

    /// <summary>
    /// 查询结构版本号，用于判断缓存的 SQL 是否仍然有效。
    /// </summary>
    private long _shapeVersion;

    /// <summary>
    /// 已缓存 SQL 对应的查询结构版本号。
    /// </summary>
    private long _cachedVersion = -1;

    /// <summary>
    /// 当前查询结构版本对应的缓存 SQL 文本。
    /// </summary>
    private string _cachedSql;

    /// <summary>
    /// 使用独立 SQL Builder 初始化查询描述。
    /// </summary>
    /// <param name="executor">绑定根查询连接、事务和诊断状态的内部执行器。</param>
    /// <param name="builder">当前查询专属的 SQL Builder。</param>
    /// <param name="parentQueryContextId">来源子查询或克隆查询的父上下文标识。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="builder"/> 为 null 时抛出。</exception>
    internal SqlQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder, string parentQueryContextId = null)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _parentQueryContextId = parentQueryContextId;
    }

    /// <summary>
    /// 获取当前查询专属的 SQL Builder。
    /// </summary>
    /// <remarks>
    /// 返回的 Builder 仅属于当前查询描述。调用方不应将其与其他线程或查询描述共享。
    /// </remarks>
    /// <returns>当前查询专属的 SQL Builder。</returns>
    internal ISqlBuilder GetBuilder()
    {
        if (_phase != QueryPhase.Draft)
            throw new InvalidOperationException("查询已冻结，不能继续修改查询描述。");
        return _builder;
    }

    /// <summary>
    /// 获取当前查询描述使用的内部计划执行器。
    /// </summary>
    /// <remarks>
    /// 派生查询描述仅可将其传递给共享同一 Builder 的后继描述，不能直接执行或替换执行器。
    /// </remarks>
    internal ISqlQueryPlanExecutor Executor => _executor;

    /// <summary>
    /// 获取当前查询上下文标识。
    /// </summary>
    internal string QueryContextId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 创建当前查询的父查询上下文标识。
    /// </summary>
    private readonly string _parentQueryContextId;

    /// <inheritdoc />
    /// <returns>当前查询使用的 SQL Builder。</returns>
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => _builder;

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => Touch();

    /// <summary>
    /// 生成当前查询的 SQL 文本。
    /// </summary>
    /// <returns>当前 Builder 渲染出的 SQL 文本。</returns>
    public string ToSql() => RenderSql();

    /// <summary>
    /// 标记查询结构已发生变化并使缓存 SQL 失效。
    /// </summary>
    internal void Touch()
    {
        _shapeVersion++;
        _cachedVersion = -1;
        _cachedSql = null;
    }

    /// <summary>
    /// 渲染当前查询 SQL，并在不需要执行快照时复用缓存结果。
    /// </summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    internal string RenderSql()
    {
        if (SqlBuilderRuntimeBridge.RequiresExecutionSnapshot(_builder))
            return _builder.ToSql();
        if (_cachedVersion == _shapeVersion && _cachedSql != null)
            return _cachedSql;
        _cachedSql = _builder.ToSql();
        _cachedVersion = _shapeVersion;
        return _cachedSql;
    }

    /// <summary>
    /// 克隆当前查询描述及其 Builder 状态。
    /// </summary>
    /// <returns>拥有独立 Builder 状态的查询描述副本。</returns>
    internal SqlQuery Clone()
    {
        var clone = new SqlQuery(_executor, _builder.Clone(), QueryContextId)
        {
            _splitOn = _splitOn
        };
        return clone;
    }

    /// <summary>
    /// 设置 Dapper 多映射使用的分段列名称。
    /// </summary>
    /// <param name="splitOn">分段列名称，多个分段列使用逗号分隔。</param>
    /// <returns>当前查询描述。</returns>
    public SqlQuery SplitOn(string splitOn)
    {
        EnsureDraft();
        if (string.IsNullOrWhiteSpace(splitOn))
            throw new ArgumentException("多映射分段列不能为空。", nameof(splitOn));
        _splitOn = splitOn;
        Touch();
        return this;
    }

    /// <summary>
    /// 同步执行当前 Fluent 查询并完整物化指定类型的结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    public List<TResult> ToList<TResult>(int? timeout = null) => _executor.ToList<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的唯一结果；没有结果时返回类型默认值。</returns>
    public TResult ToEntity<TResult>(int? timeout = null) => _executor.SingleOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null) => _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询，并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终映射结果列表。</returns>
    public List<TResult> ToList<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null) =>
        _executor.ToList(GetPlan(), map, timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行结果。</returns>
    public TResult First<TResult>(int? timeout = null) => _executor.First<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>第一行或默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null) =>
        _executor.FirstOrDefault<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行。</returns>
    public TResult Single<TResult>(int? timeout = null) => _executor.Single<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>首行首列值；无结果时返回默认值。</returns>
    public TResult Scalar<TResult>(int? timeout = null) => _executor.Scalar<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 同步执行当前 Fluent 查询并返回指定页的数据和总行数。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页信息和结果行的集合。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _executor.ToPage<TResult>(GetPlan(), pager, timeout);

    /// <summary>
    /// 以同步流方式执行当前 Fluent 查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) =>
        _executor.AsEnumerable<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 异步执行当前 Fluent 查询并完整物化指定类型的结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.SingleOrDefaultAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为两个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将两个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TResult>(Func<TFirst, TSecond, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为三个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将三个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TResult>(Func<TFirst, TSecond, TThird, TResult> map,
        int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为四个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将四个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为五个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将五个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为六个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将六个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询，并将每行映射为七个对象后完整物化结果集。
    /// </summary>
    /// <typeparam name="TFirst">第一个对象映射类型。</typeparam>
    /// <typeparam name="TSecond">第二个对象映射类型。</typeparam>
    /// <typeparam name="TThird">第三个对象映射类型。</typeparam>
    /// <typeparam name="TFourth">第四个对象映射类型。</typeparam>
    /// <typeparam name="TFifth">第五个对象映射类型。</typeparam>
    /// <typeparam name="TSixth">第六个对象映射类型。</typeparam>
    /// <typeparam name="TSeventh">第七个对象映射类型。</typeparam>
    /// <typeparam name="TResult">映射委托返回的结果类型。</typeparam>
    /// <param name="map">将七个对象映射为结果的委托。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终映射结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult>(
        Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TResult> map, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.FirstAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行或默认值的异步操作。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstOrDefaultAsync<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作；无结果时返回默认值。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ScalarAsync<TResult>(GetPlan(), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并返回指定页的数据和总行数。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含分页信息和结果行集合的异步操作。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToPageAsync<TResult>(GetPlan(), pager, timeout,
        cancellationToken);

    /// <summary>
    /// 以异步流方式执行当前 Fluent 查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结果行异步流。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.AsAsyncEnumerable<TResult>(GetPlan(), timeout,
        cancellationToken);

    /// <summary>
    /// 获取当前 Fluent SQL Builder 对应的内部执行计划。
    /// </summary>
    /// <returns>引用当前 Builder 独立快照的查询计划。</returns>
    private SqlQueryPlan GetPlan()
    {
        if (_phase == QueryPhase.Draft)
            _phase = QueryPhase.Frozen;
        var plan = SqlQueryPlan.Create(_builder.Clone(), _splitOn);
        plan.SetContext(QueryContextId, _parentQueryContextId ?? (_builder as SqlBuilderBase)?.ParentQueryContextId);
        plan.SetLifecycleCallbacks(BeginExecution, CompleteExecution);
        return plan;
    }

    /// <summary>
    /// 标记查询开始执行并获取执行租约。
    /// </summary>
    private void BeginExecution()
    {
        if (Interlocked.CompareExchange(ref _executionLease, 1, 0) != 0)
            throw new InvalidOperationException("当前查询正在执行，不能并发执行同一查询描述。");
        _phase = QueryPhase.Executing;
    }

    /// <summary>
    /// 标记查询执行完成并释放执行租约。
    /// </summary>
    private void CompleteExecution()
    {
        _phase = QueryPhase.Completed;
        Volatile.Write(ref _executionLease, 0);
    }

    /// <summary>
    /// 确认当前查询仍处于可修改的草稿阶段。
    /// </summary>
    private void EnsureDraft()
    {
        if (_phase != QueryPhase.Draft)
            throw new InvalidOperationException("查询已冻结，不能继续修改查询描述。");
    }
}
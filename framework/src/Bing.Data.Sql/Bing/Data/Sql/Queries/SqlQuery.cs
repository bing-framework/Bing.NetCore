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
    /// 草稿期承载查询结构；首次终结成功后替换为隔离副本，供校验、预览及克隆统一读取。
    /// </summary>
    private ISqlBuilder _builder;

    /// <summary>静态查询复用冻结 IR；执行标识、回调和租约仍由每次创建的计划承载。</summary>
    private SqlQueryIr _frozenQueryIr;

    /// <summary>
    /// 执行当前查询计划的根查询内部执行器。
    /// </summary>
    private readonly ISqlQueryPlanExecutor _executor;

    /// <summary>
    /// Dapper 多映射的分段列名称。
    /// </summary>
    private string _splitOn = "Id";

    /// <summary>
    /// 获取多映射查询使用的分割列文本。
    /// </summary>
    internal string SplitOnValue => _splitOn;
    /// <summary>
    /// 获取当前查询使用的 SQL 构建器。
    /// </summary>
    internal ISqlBuilder ReadBuilder => _builder;

    /// <summary>
    /// 查询结构是否已冻结；活动执行由独立租约保护。
    /// </summary>
    private bool _isFrozen;

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

    /// <summary>与缓存 SQL 一起失效的不可变 Select 渲染边界。</summary>
    private SqlQuerySelectShape _cachedSelectShape;

    /// <summary>
    /// 初始化一个 <see cref="SqlQuery"/> 类型的实例。
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
        if (_isFrozen)
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
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => _builder;

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => Touch();

    /// <summary>
    /// 生成当前查询的 SQL 文本。
    /// </summary>
    /// <returns>当前 Builder 渲染出的 SQL 文本。</returns>
    public string ToSql() => RenderSql();

    /// <summary>
    /// 以原子方式修改查询构建器并返回操作结果。
    /// </summary>
    /// <param name="change">执行查询修改的函数。</param>
    /// <typeparam name="TResult">查询修改操作的结果类型。</typeparam>
    /// <returns>查询修改函数返回的结果。</returns>
    /// <remarks>
    /// 外层修改失败时恢复原构建器、投影、版本和缓存状态。
    /// </remarks>
    internal TResult AtomicChange<TResult>(Func<TResult> change)
    {
        EnsureDraft();
        if (_mutationDepth != 0)
            return change();
        var original = _builder;
        var legacyProjection = LegacyProjectionColumns;
        var version = _shapeVersion;
        var cachedVersion = _cachedVersion;
        var cachedSql = _cachedSql;
        var cachedSelectShape = _cachedSelectShape;
        using var checkpoint = (original as SqlBuilderBase)?.BeginMutationCheckpoint();
        if (checkpoint == null)
            _builder = original.Clone();
        _mutationDepth++;
        try
        {
            var result = change();
            checkpoint?.Complete();
            return result;
        }
        catch
        {
            _builder = original;
            LegacyProjectionColumns = legacyProjection;
            _shapeVersion = version;
            _cachedVersion = cachedVersion;
            _cachedSql = cachedSql;
            _cachedSelectShape = cachedSelectShape;
            throw;
        }
        finally { _mutationDepth--; }
    }

    /// <summary>
    /// 记录当前查询上嵌套变更操作的深度。
    /// </summary>
    private int _mutationDepth;

    // 仅供尚未提供结构化投影能力的外部 Clause SPI 保留累计状态。
    /// <summary>
    /// 获取或设置旧投影 API 指定的列集合。
    /// </summary>
    internal HashSet<string> LegacyProjectionColumns { get; set; }

    /// <summary>
    /// 将当前查询构建器替换为指定构建器。
    /// </summary>
    /// <param name="builder">要采用的 SQL 构建器。</param>
    internal void AdoptBuilder(ISqlBuilder builder) => _builder = builder;

    /// <summary>
    /// 标记查询结构已发生变化并使缓存 SQL 失效。
    /// </summary>
    internal void Touch()
    {
        _shapeVersion++;
        _cachedVersion = -1;
        _cachedSql = null;
        _cachedSelectShape = null;
    }

    /// <summary>
    /// 渲染当前查询 SQL，并在不需要执行快照时复用缓存结果。
    /// </summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    internal string RenderSql()
    {
        if (_frozenQueryIr != null)
            return _frozenQueryIr.Snapshot.Sql;
        if (SqlBuilderRuntimeBridge.RequiresExecutionSnapshot(_builder))
            return _builder.ToSql();
        // 外层没有过滤器时，CTE/Union 后代仍可能读取环境值；预览不能缓存这些值。
        if (HasDynamicNestedQuery(_builder))
            return _builder.Clone().ToSql();
        if (_cachedVersion == _shapeVersion && _cachedSql != null)
            return _cachedSql;
        if (_builder is SqlBuilderBase sqlBuilder)
        {
            var render = sqlBuilder.CreateRenderSnapshot();
            _cachedSql = render.Sql;
            _cachedSelectShape = render.SelectShape;
        }
        else
        {
            _cachedSql = _builder.ToSql();
            _cachedSelectShape = null;
        }
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
            _splitOn = _splitOn,
            LegacyProjectionColumns = LegacyProjectionColumns == null ? null : new HashSet<string>(LegacyProjectionColumns)
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
    /// 同步执行查询并返回结果集。
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
        _executor.ToPage<TResult>(GetPlan(forPaging: true), pager, timeout);

    /// <summary>
    /// 以同步流方式执行当前 Fluent 查询。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>结果行同步流。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) =>
        _executor.AsEnumerable<TResult>(GetPlan(), timeout);

    /// <summary>
    /// 异步执行查询并返回结果集。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) =>
        _executor.ToListAsync<TResult>(GetPlan(cancellationToken), timeout, cancellationToken);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.SingleOrDefaultAsync<TResult>(GetPlan(cancellationToken), timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        _executor.ToListAsync(GetPlan(cancellationToken), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        _executor.ToListAsync(GetPlan(cancellationToken), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(cancellationToken), map, timeout, cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(cancellationToken), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(cancellationToken), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行查询并返回结果集。
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
        CancellationToken cancellationToken = default) => _executor.ToListAsync(GetPlan(cancellationToken), map, timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行结果的异步操作。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.FirstAsync<TResult>(GetPlan(cancellationToken), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示第一行或默认值的异步操作。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.FirstOrDefaultAsync<TResult>(GetPlan(cancellationToken), timeout,
        cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行的异步操作。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.SingleAsync<TResult>(GetPlan(cancellationToken), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并获取首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示首行首列值的异步操作；无结果时返回默认值。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _executor.ScalarAsync<TResult>(GetPlan(cancellationToken), timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Fluent 查询并返回指定页的数据和总行数。
    /// </summary>
    /// <typeparam name="TResult">结果行映射类型。</typeparam>
    /// <param name="pager">分页参数；传入 null 时使用当前 Builder 的分页配置。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示包含分页信息和结果行集合的异步操作。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _executor.ToPageAsync<TResult>(GetPlan(cancellationToken, forPaging: true), pager, timeout,
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
    /// <param name="forPaging">是否需要在原始查询作用域派生 Count 和数据页。</param>
    /// <returns>包含冻结 SQL 和必要分页规划来源的查询计划。</returns>
    internal SqlQueryPlan GetPlan(bool forPaging = false)
    {
        var snapshot = _isFrozen ? _builder : _builder.Clone();
        var queryIr = _frozenQueryIr;
        ISqlBuilder planningBuilder = null;
        if (queryIr == null || SqlBuilderRuntimeBridge.RequiresExecutionSnapshot(snapshot))
        {
            if (((ISqlQueryClauseAccessor)snapshot).SelectClause is Bing.Data.Sql.Builders.Clauses.SelectClause select)
                select.ValidateStructure();
            // Builder 在此处只负责生成一次 SQL；执行器后续只消费冻结快照，避免执行阶段重复渲染。
            SqlBuilderExecutionSnapshot executionSnapshot;
            var requiresOwnSnapshot = SqlBuilderRuntimeBridge.RequiresExecutionSnapshot(snapshot);
            var hasDynamicNestedQuery = HasDynamicNestedQuery(snapshot);
            var requiresExecutionSnapshot = requiresOwnSnapshot || hasDynamicNestedQuery;
            var requiresPlanning = forPaging || SqlBuilderRuntimeBridge.RequiresPlanningBuilder(snapshot);
            if ((requiresPlanning || requiresExecutionSnapshot) &&
                snapshot is SqlBuilderBase sqlBuilder)
            {
                // SQL 和派生来源必须取自同一次渲染，以免动态过滤在 Count 与数据页之间发生变化。
                // 外层没有过滤器时，基类不会自行克隆；先隔离仍需按次取值的后代。
                if (hasDynamicNestedQuery && !requiresOwnSnapshot)
                    sqlBuilder = (SqlBuilderBase)sqlBuilder.Clone();
                var rendered = sqlBuilder.CreateRenderSnapshot();
                executionSnapshot = SqlBuilderRuntimeBridge.CreateExecutionSnapshot(rendered);
                planningBuilder = rendered.Builder;
            }
            else
            {
                executionSnapshot = !requiresExecutionSnapshot &&
                                    _cachedVersion == _shapeVersion && _cachedSql != null
                    ? SqlBuilderRuntimeBridge.CreateExecutionSnapshot(snapshot, _cachedSql, _cachedSelectShape)
                    : SqlBuilderRuntimeBridge.CreateExecutionSnapshot(snapshot);
                // 完整 IR 会在计划构造时释放来源；自定义子句、参数管理器或 SQL 尾部仍需原结构。
                planningBuilder = snapshot;
            }
            queryIr = SqlQueryIr.Create(executionSnapshot, System.Data.CommandType.Text, snapshot.Pager);
            // 必要的第三方回退和动态过滤仍按次渲染；只缓存无需 Builder 即可派生的静态 IR。
            if (!requiresExecutionSnapshot && executionSnapshot.CanDeriveAnyPage)
                _frozenQueryIr = queryIr;
        }
        var plan = SqlQueryPlan.Create(queryIr, _splitOn, planningBuilder);
        // 仅成功生成计划后发布结构；失败时保留可编辑草稿，重试重新捕获其最新修改。
        // 不再同时持有原草稿与冻结副本，所有读入口和 Clone 都使用这一份结构。
        if (!_isFrozen)
        {
            _builder = snapshot;
            _cachedVersion = -1;
            _cachedSql = null;
            _cachedSelectShape = null;
        }
        _isFrozen = true;
        plan.SetContext(QueryContextId, _parentQueryContextId ?? (_builder as SqlBuilderBase)?.ParentQueryContextId);
        plan.SetLifecycleCallbacks(BeginExecution, CompleteExecution);
        return plan;
    }

    /// <summary>生成模板沿用相同的冻结 IR、查询标识及执行租约，不创建第二套执行生命周期。</summary>
    internal SqlQueryPlan GetGeneratedTemplatePlan(SqlBuilderExecutionSnapshot snapshot)
    {
        _frozenQueryIr ??= SqlQueryIr.Create(snapshot, System.Data.CommandType.Text, _builder.Pager);
        var plan = SqlQueryPlan.Create(_frozenQueryIr, _splitOn, null);
        _isFrozen = true;
        plan.SetContext(QueryContextId, _parentQueryContextId ?? (_builder as SqlBuilderBase)?.ParentQueryContextId);
        plan.SetLifecycleCallbacks(BeginExecution, CompleteExecution);
        return plan;
    }

    /// <summary>恢复延迟生成链的等价结构，供读取或克隆使用；冻结查询继续保留原 IR 和租约。</summary>
    internal void MaterializeGeneratedTemplate(ISqlBuilder builder)
    {
        _builder = builder;
        if (!_isFrozen) Touch();
    }

    /// <summary>CTE/集合仍在终结时渲染，后代的动态过滤不能随外层静态 IR 缓存。</summary>
    private static bool HasDynamicNestedQuery(ISqlBuilder builder)
    {
        if (builder is ICteAccessor cte)
            foreach (var item in cte.CteItems)
                if (item.Builder != null && RequiresFreshNestedQuery(item.Builder))
                    return true;
        if (builder is IUnionAccessor union)
            foreach (var item in union.UnionItems)
                if (item.Builder != null && RequiresFreshNestedQuery(item.Builder))
                    return true;
        return false;
    }

    /// <summary>第三方非基类子查询没有可验证的动态输入合同，保持原按次渲染行为。</summary>
    private static bool RequiresFreshNestedQuery(ISqlBuilder builder) => builder is not SqlBuilderBase ||
        SqlBuilderRuntimeBridge.RequiresExecutionSnapshot(builder) || HasDynamicNestedQuery(builder);

    /// <summary>
    /// 创建异步执行计划；预取消必须优先于 SQL 结构校验和渲染。
    /// </summary>
    /// <param name="cancellationToken">当前异步执行的取消令牌。</param>
    /// <param name="forPaging">是否需要分页派生来源。</param>
    /// <returns>已冻结的查询计划。</returns>
    internal SqlQueryPlan GetPlan(CancellationToken cancellationToken, bool forPaging = false)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            // 预取消交给执行器处理，以便它标记共享 UnitOfWork 为 rollback-only；
            // 此处只冻结 Builder 引用，不触发结构校验、SQL 渲染或连接创建。
            var cancelledSnapshot = _isFrozen ? _builder : _builder.Clone();
            var cancelledPlan = SqlQueryPlan.Create(cancelledSnapshot, _splitOn);
            cancelledPlan.SetContext(QueryContextId, _parentQueryContextId ??
                (cancelledSnapshot as SqlBuilderBase)?.ParentQueryContextId);
            cancelledPlan.SetLifecycleCallbacks(BeginExecution, CompleteExecution);
            _builder = cancelledSnapshot;
            _cachedVersion = -1;
            _cachedSql = null;
            _cachedSelectShape = null;
            _isFrozen = true;
            return cancelledPlan;
        }
        return GetPlan(forPaging);
    }

    /// <summary>
    /// 标记查询开始执行并获取执行租约。
    /// </summary>
    private void BeginExecution()
    {
        if (Interlocked.CompareExchange(ref _executionLease, 1, 0) != 0)
            throw new InvalidOperationException("当前查询正在执行，不能并发执行同一查询描述。");
    }

    /// <summary>
    /// 释放当前执行租约，查询结构继续保持冻结。
    /// </summary>
    private void CompleteExecution() => Volatile.Write(ref _executionLease, 0);

    /// <summary>
    /// 确认当前查询仍处于可修改的草稿阶段。
    /// </summary>
    private void EnsureDraft()
    {
        if (_isFrozen)
            throw new InvalidOperationException("查询已冻结，不能继续修改查询描述。");
    }
}

using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// Lambda 查询描述的内部执行核心。
/// </summary>
internal sealed partial class SqlLambdaQueryCore : ISqlQueryBuilderAccessor
{
    /// <summary>
    /// 承载当前 Lambda 查询状态和执行逻辑的查询对象。
    /// </summary>
    private readonly SqlQuery _query;

    /// <summary>
    /// 初始化一个 <see cref="SqlLambdaQueryCore"/> 类型的实例。
    /// </summary>
    /// <param name="executor">查询计划执行器。</param>
    /// <param name="builder">当前查询使用的 SQL 生成器。</param>
    internal SqlLambdaQueryCore(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        _query = new SqlQuery(executor, builder);

    /// <summary>
    /// 初始化一个 <see cref="SqlLambdaQueryCore"/> 类型的实例。
    /// </summary>
    /// <param name="query">已有查询对象。</param>
    private SqlLambdaQueryCore(SqlQuery query) => _query = query ?? throw new ArgumentNullException(nameof(query));

    /// <summary>
    /// 克隆当前查询核心及其查询状态。
    /// </summary>
    /// <returns>拥有独立查询状态的查询核心。</returns>
    internal SqlLambdaQueryCore Clone() { MaterializeTemplate(); return new(_query.Clone()); }

    /// <summary>
    /// 获取当前查询使用的 SQL 生成器。
    /// </summary>
    /// <returns>当前查询的 SQL 生成器。</returns>
    internal ISqlBuilder GetBuilder() { MaterializeTemplate(); return _query.GetBuilder(); }

    /// <summary>
    /// 获取当前查询计划执行器。
    /// </summary>
    internal ISqlQueryPlanExecutor Executor => _query.Executor;
    /// <summary>
    /// 获取当前 Lambda 查询的生成执行计划。
    /// </summary>
    /// <param name="forPaging">是否需要在原查询作用域派生分页计划。</param>
    /// <returns>当前查询对应的 SQL 查询计划。</returns>
    internal SqlQueryPlan GeneratedPlan(bool forPaging = false) { MaterializeTemplate(); return _query.GetPlan(forPaging); }
    /// <summary>
    /// 获取当前查询使用的 SQL 构建器。
    /// </summary>
    internal ISqlBuilder ReadBuilder { get { MaterializeTemplate(); return _query.ReadBuilder; } }

    /// <summary>仅完整生成链使用的请求状态，普通查询没有额外状态分配。</summary>
    internal SqlGeneratedQueryTemplate.State TemplateState { get; set; }

    internal void MaterializeTemplate()
    {
        var state = TemplateState;
        if (state == null) return;
        // 回放只恢复等价的 Builder 表示，不改变冻结 IR；失败时也保留原延迟状态。
        var builder = _query.ReadBuilder.Clone();
        state.Replay(SqlQueryRuntimeFactory.CreateLambdaQuery(_query.Executor, builder));
        _query.MaterializeGeneratedTemplate(builder);
        TemplateState = null;
    }

    internal bool TryGeneratedTemplatePlan(Type resultType, out SqlQueryPlan plan, out Dictionary<string, int>[] ordinals)
    {
        plan = null;
        ordinals = null;
        if (TemplateState == null || !TemplateState.TryPrepare(resultType, out var snapshot, out ordinals)) return false;
        plan = _query.GetGeneratedTemplatePlan(snapshot);
        return true;
    }
    /// <summary>
    /// 获取生成的多映射分割列文本。
    /// </summary>
    internal string GeneratedSplitOn => _query.SplitOnValue;

    /// <summary>
    /// 获取当前查询上下文标识。
    /// </summary>
    internal string QueryContextId => _query.QueryContextId;

    /// <summary>
    /// 生成当前查询的 SQL 文本。
    /// </summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    public string ToSql()
    {
        if (TemplateState != null && TemplateState.TryPrepare(typeof(void), out var snapshot, out _)) return snapshot.Sql;
        MaterializeTemplate();
        return _query.RenderSql();
    }

    /// <summary>
    /// 标记查询结构已成功变更。
    /// </summary>
    internal void Touch() => _query.Touch();

    /// <summary>
    /// 以原子操作方式修改查询并返回操作结果。
    /// </summary>
    /// <param name="change">执行查询修改并生成结果的函数。</param>
    /// <typeparam name="TResult">查询修改操作的结果类型。</typeparam>
    /// <returns>查询修改函数返回的结果。</returns>
    internal TResult AtomicChange<TResult>(Func<TResult> change) { MaterializeTemplate(); return _query.AtomicChange(change); }

    /// <summary>
    /// 同步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果列表。</returns>
    /// <remarks>
    /// 同步执行当前 Lambda 查询并完整物化指定结果类型。
    /// </remarks>
    public List<TResult> ToList<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToList<TResult>(timeout);
    }

    /// <summary>
    /// 根据结果原型生成投影后同步执行列表查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="prototype">仅用于确定投影成员，不读取实例属性值。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>最终结果列表。</returns>
    internal List<TResult> ToList<TResult>(TResult prototype, int? timeout = null) where TResult : class
    {
        ApplyPrototypeProjection(prototype);
        ValidateProjectionResult<TResult>();
        return _query.ToList<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并返回至多一行结果。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的实体；没有结果时返回类型默认值。</returns>
    internal TResult ToEntity<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToEntity<TResult>(timeout);
    }

    /// <summary>
    /// 根据结果原型生成投影后同步执行单行查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="prototype">仅用于确定投影成员，不读取实例属性值。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    internal TResult To<TResult>(TResult prototype, int? timeout = null) where TResult : class
    {
        ApplyPrototypeProjection(prototype);
        ValidateProjectionResult<TResult>();
        return _query.ToEntity<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前查询并获取至多一行结果；无结果时返回默认值。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>唯一结果行或默认值。</returns>
    internal TResult To<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToEntity<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并获取指定结果类型的第一行。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行。</returns>
    public TResult First<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.First<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行；没有结果时返回类型默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.FirstOrDefault<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并获取指定结果类型的唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的唯一结果。</returns>
    public TResult Single<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.Single<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并获取指定结果类型的首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询得到的标量值。</returns>
    public TResult Scalar<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        ValidateScalarProjectionResult<TResult>();
        return _query.Scalar<TResult>(timeout);
    }

    /// <summary>
    /// 同步执行当前 Lambda 查询并返回指定结果类型的分页结果。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页数据和分页信息的结果。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToPage<TResult>(pager, timeout);
    }

    /// <summary>
    /// 以同步流方式执行当前 Lambda 查询并映射为指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>按需读取查询结果的可枚举序列。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null)
    {
        ValidateProjectionResult<TResult>();
        return _query.AsEnumerable<TResult>(timeout);
    }

    /// <summary>
    /// 异步执行 Lambda 查询并物化结果列表。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    /// <remarks>
    /// 异步执行当前 Lambda 查询并完整物化指定结果类型。
    /// </remarks>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToListAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 根据结果原型生成投影后异步执行列表查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="prototype">仅用于确定投影成员，不读取实例属性值。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示最终结果列表的异步操作。</returns>
    internal Task<List<TResult>> ToListAsync<TResult>(TResult prototype, int? timeout = null,
        CancellationToken cancellationToken = default) where TResult : class
    {
        ApplyPrototypeProjection(prototype);
        ValidateProjectionResult<TResult>();
        return _query.ToListAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并返回至多一行结果。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询实体的异步任务；没有结果时任务结果为类型默认值。</returns>
    internal Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToEntityAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 根据结果原型生成投影后异步执行单行查询。
    /// </summary>
    /// <typeparam name="TResult">结果 DTO 类型。</typeparam>
    /// <param name="prototype">仅用于确定投影成员，不读取实例属性值。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行或默认值的异步操作。</returns>
    internal Task<TResult> ToAsync<TResult>(TResult prototype, int? timeout = null,
        CancellationToken cancellationToken = default) where TResult : class
    {
        ApplyPrototypeProjection(prototype);
        ValidateProjectionResult<TResult>();
        return _query.ToEntityAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前查询并获取至多一行结果；无结果时返回默认值。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>表示唯一结果行或默认值的异步操作。</returns>
    internal Task<TResult> ToAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToEntityAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并获取指定结果类型的第一行。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.FirstAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并获取指定结果类型的唯一一行。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询唯一结果的异步任务。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.SingleAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并获取指定结果类型的首行首列值。
    /// </summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询标量值的异步任务。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        ValidateScalarProjectionResult<TResult>();
        return _query.ScalarAsync<TResult>(timeout, cancellationToken);
    }

    /// <summary>
    /// 异步执行当前 Lambda 查询并返回指定结果类型的分页结果。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页数据和分页信息的异步任务。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);
    }

    /// <summary>
    /// 以异步流方式执行当前 Lambda 查询并映射为指定结果类型。
    /// </summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按需读取查询结果的异步可枚举序列。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectionResult<TResult>();
        return _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);
    }

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => GetBuilder();

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => Touch();

    /// <summary>
    /// 验证指定泛型标量结果类型与当前投影兼容。
    /// </summary>
    /// <typeparam name="TResult">查询的标量结果类型。</typeparam>
    private void ValidateScalarProjectionResult<TResult>() =>
        (((ISqlQueryClauseAccessor)_query.ReadBuilder).SelectClause as SelectClause)?
            .ValidateScalarResult(typeof(TResult));

    /// <summary>
    /// 追加查询条件。
    /// </summary>
    /// <param name="expression">条件表达式。</param>
    /// <returns>追加条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore WhereCore(LambdaExpression expression)
    {
        return WhereCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 追加查询条件。
    /// </summary>
    /// <param name="expression">条件表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore WhereCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        accessor.WhereClause.Where(GetFromClause(accessor).ResolveMultiSourcePredicate(expression, sources));
        Touch();
        return this;
    }

    /// <summary>
    /// 使用显式表源解析单列参数条件并追加到 Where 子句。
    /// </summary>
    /// <param name="column">返回条件列的 Lambda 表达式。</param>
    /// <param name="value">条件值。</param>
    /// <param name="operator">条件运算符。</param>
    /// <param name="source">显式绑定的查询表源。</param>
    /// <returns>追加条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore WhereValueCore(LambdaExpression column, object value, Operator @operator,
        TableSource source)
    {
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var condition = GetFromClause(accessor).ResolveMultiSourceValueCondition(column, source, value, @operator);
        accessor.WhereClause.Where(condition);
        Touch();
        return this;
    }

    /// <summary>
    /// 在独立 Builder 候选上解析并一次性提交嵌套条件组。
    /// </summary>
    /// <param name="configure">配置嵌套条件组的委托。</param>
    /// <returns>追加条件组后的当前查询核心。</returns>
    internal SqlLambdaQueryCore WhereGroupCore(Action<ISqlConditionGroup> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));
        var sourceBuilder = GetBuilder();
        var candidate = sourceBuilder.Clone();
        var candidateAccessor = (ISqlQueryClauseAccessor)candidate;
        var group = new SqlConditionGroup((expression, aliases) =>
        {
            var sources = ResolveConditionSources(candidateAccessor, expression, aliases);
            return GetFromClause(candidateAccessor).ResolveMultiSourcePredicate(expression, sources);
        });
        configure(group);
        if (group.Condition == null)
            return this;
        candidateAccessor.WhereClause.Where(group.Condition);
        _query.AdoptBuilder(candidate);
        Touch();
        return this;
    }

    /// <summary>
    /// 根据条件表达式参数和别名解析绑定的表源。
    /// </summary>
    /// <param name="accessor">SQL 子句访问器。</param>
    /// <param name="expression">待解析的条件表达式。</param>
    /// <param name="aliases">可选的表源别名列表。</param>
    /// <returns>与表达式参数顺序对应的表源集合。</returns>
    private static IReadOnlyList<TableSource> ResolveConditionSources(ISqlQueryClauseAccessor accessor,
        LambdaExpression expression, IReadOnlyList<string> aliases)
    {
        return SqlLambdaSourceResolver.Resolve(accessor, expression, aliases);
    }

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <param name="expression">投影表达式。</param>
    /// <returns>设置投影后的当前查询核心。</returns>
    internal SqlLambdaQueryCore SelectCore(LambdaExpression expression)
    {
        return SelectCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 设置查询投影。
    /// </summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>设置投影后的当前查询核心。</returns>
    internal SqlLambdaQueryCore SelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        ApplyProjection(expression, sources);
        return this;
    }

    /// <summary>
    /// 追加指定来源的投影列。
    /// </summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void AppendSelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        ApplyProjection(expression, sources, legacyAppend: true);
    }

    /// <summary>
    /// 追加单来源 DTO 投影列。
    /// </summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void AppendSelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        ApplyProjection(expression, sources, legacyAppend: true);
    }

    /// <summary>
    /// 设置 DTO 查询投影。
    /// </summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    internal void SelectTypedCore(LambdaExpression expression)
    {
        SelectTypedCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 设置 DTO 查询投影。
    /// </summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void SelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        ApplyProjection(expression, sources);
    }

    /// <summary>
    /// 使用严格 DTO 成员初始化投影创建冻结的类型化派生表。
    /// </summary>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="expression">DTO 成员初始化投影表达式。</param>
    /// <param name="alias">派生表别名。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>包含冻结查询结构和投影成员映射的类型化派生表。</returns>
    internal SqlSubquery<TProjection> SelectSubqueryCore<TProjection>(LambdaExpression expression, string alias,
        IReadOnlyList<TableSource> sources)
        where TProjection : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, sources,
            out var projectedMembers);
        var builder = GetBuilder().Clone();
        var subqueryAccessor = (ISqlQueryClauseAccessor)builder;
        builder.ClearSelect();
        subqueryAccessor.SelectClause.Select(string.Join(", ", columns));
        if (builder is SqlBuilderBase { HasLimit: false })
            builder.ClearOrderBy();
        var sqlBuilder = builder as SqlBuilderBase;
        var context = sqlBuilder?.GetDatabaseContext();
        var dataSourceKey = context?.DataSource?.Key ?? context?.DbKey;
        return new SqlSubquery<TProjection>(builder, alias, projectedMembers, builder.Provider?.Key, dataSourceKey,
            context?.MappingProfile, context?.TenantId, sqlBuilder?.GetDatabaseIdentity(), sqlBuilder?.GetExecutionScope(),
            _query.QueryContextId);
    }

    /// <summary>
    /// 设置分组列。
    /// </summary>
    /// <param name="expression">分组表达式。</param>
    /// <returns>追加分组列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore GroupByCore(LambdaExpression expression)
    {
        return GroupByCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 设置分组列。
    /// </summary>
    /// <param name="expression">分组表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加分组列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore GroupByCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        if (accessor.GroupByClause is Bing.Data.Sql.Builders.Clauses.GroupByClause clause)
            clause.AddLogicalColumns(SqlFunctionProjectionParser.Parse(expression, sources));
        else
            GetGroupByClause(accessor).AppendBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources));
        Touch();
        return this;
    }

    /// <summary>
    /// 设置 Having 条件。
    /// </summary>
    /// <param name="expression">Having 条件表达式。</param>
    /// <returns>追加 Having 条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore HavingCore(LambdaExpression expression)
    {
        return HavingCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 设置 Having 条件。
    /// </summary>
    /// <param name="expression">Having 条件表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加 Having 条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore HavingCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        if (SqlFunctionProjectionParser.ContainsAggregate(expression))
        {
            if (accessor.GroupByClause is not Bing.Data.Sql.Builders.Clauses.GroupByClause clause)
                throw new NotSupportedException("当前 Provider 不支持结构化聚合条件。");
            clause.SetFunctionHaving(expression, sources);
        }
        else
            GetGroupByClause(accessor).SetBoundHaving(GetFromClause(accessor).ResolveMultiSourcePredicate(expression, sources));
        Touch();
        return this;
    }

    /// <summary>
    /// 设置排序列。
    /// </summary>
    /// <param name="expression">排序表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>追加排序列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore OrderByCore(LambdaExpression expression, bool desc)
    {
        return OrderByCore(expression, desc, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>
    /// 设置排序列。
    /// </summary>
    /// <param name="expression">排序表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加排序列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore OrderByCore(LambdaExpression expression, bool desc,
        IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        if (accessor.OrderByClause is Bing.Data.Sql.Builders.Clauses.OrderByClause clause)
            clause.AddLogicalColumns(SqlFunctionProjectionParser.Parse(expression, sources), desc);
        else
            GetOrderByClause(accessor).AppendBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources), desc);
        Touch();
        return this;
    }

    /// <summary>
    /// 添加内连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void JoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().Join<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>
    /// 添加内连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void JoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class
    {
        JoinAndTouch(() => GetJoinClause().Join<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), SqlLambdaSourceResolver.JoinAlias((ISqlQueryClauseAccessor)GetBuilder(), alias, predicate.Parameters[1].Name), schema));
    }

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void LeftJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), SqlLambdaSourceResolver.JoinAlias((ISqlQueryClauseAccessor)GetBuilder(), alias, predicate.Parameters[1].Name), schema));

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void RightJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), SqlLambdaSourceResolver.JoinAlias((ISqlQueryClauseAccessor)GetBuilder(), alias, predicate.Parameters[1].Name), schema));

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void FullJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), SqlLambdaSourceResolver.JoinAlias((ISqlQueryClauseAccessor)GetBuilder(), alias, predicate.Parameters[1].Name), schema));

    /// <summary>
    /// 将二元连接表达式扩展为包含当前查询已有来源的参数布局。
    /// </summary>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <returns>参数布局与当前查询来源一致的连接表达式。</returns>
    private LambdaExpression ExpandJoinPredicate(LambdaExpression predicate, TableSource leftSource)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));
        if (leftSource == null)
            throw new ArgumentNullException(nameof(leftSource));
        if (predicate.Parameters.Count != 2)
            throw new ArgumentException("二元 Join 谓词必须包含两个参数。", nameof(predicate));
        var sources = GetBoundSources((ISqlQueryClauseAccessor)GetBuilder());
        if (sources.Contains(leftSource) == false)
            throw new InvalidOperationException("Join 左侧来源不属于当前查询。");
        var parameters = new List<ParameterExpression>(sources.Count + 1);
        foreach (var source in sources)
        {
            parameters.Add(source == leftSource
                ? predicate.Parameters[0]
                : Expression.Parameter(source.EntityType, source.Alias));
        }
        parameters.Add(predicate.Parameters[1]);
        return Expression.Lambda(predicate.Body, parameters);
    }

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void LeftJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void RightJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void FullJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>
    /// 添加内连接。
    /// </summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void JoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().Join(GetFromClause(), subquery, predicate));

    /// <summary>
    /// 添加内连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void JoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().Join(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void LeftJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin(GetFromClause(), subquery, predicate));

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void LeftJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void RightJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin(GetFromClause(), subquery, predicate));

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void RightJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void FullJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin(GetFromClause(), subquery, predicate));

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void FullJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>
    /// 添加交叉连接。
    /// </summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void CrossJoinCore<TJoin>(string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().CrossJoin<TJoin>(alias, schema));

    /// <summary>
    /// 添加交叉连接。
    /// </summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待交叉连接的派生表。</param>
    internal void CrossJoinCore<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().CrossJoin(subquery));

    /// <summary>
    /// 替换当前查询的投影列。
    /// </summary>
    /// <param name="columns">新的投影列 SQL 文本。</param>
    internal void ReplaceSelect(string columns)
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        builder.ReplaceSelect(select => select.Select(columns));
    }

    /// <summary>
    /// 设置查询结果的跳过行数。
    /// </summary>
    /// <param name="count">要跳过的行数。</param>
    internal void SkipCore(int count) { GetBuilder().Skip(count); Touch(); }

    /// <summary>
    /// 设置查询结果的获取行数上限。
    /// </summary>
    /// <param name="count">最多获取的行数。</param>
    internal void TakeCore(int count) { GetBuilder().Take(count); Touch(); }

    /// <summary>
    /// 执行连接操作并标记查询结构已变更。
    /// </summary>
    /// <param name="operation">待执行的连接操作。</param>
    internal void JoinAndTouch(Action operation)
    {
        operation();
        Touch();
    }

    /// <summary>
    /// 获取 From 子句。
    /// </summary>
    /// <returns>当前查询的根来源子句。</returns>
    internal ISqlMultiSourceFromClause GetFromClause() => GetFromClause((ISqlQueryClauseAccessor)GetBuilder());

    /// <summary>
    /// 获取 Join 子句。
    /// </summary>
    /// <returns>当前查询的连接子句。</returns>
    internal ISqlMultiSourceJoinClause GetJoinClause() => GetJoinClause((ISqlQueryClauseAccessor)GetBuilder());

    /// <summary>
    /// 获取 From 子句。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>查询的根来源子句。</returns>
    internal static ISqlMultiSourceFromClause GetFromClause(ISqlQueryClauseAccessor accessor) =>
        accessor?.FromClause as ISqlMultiSourceFromClause ??
        throw new NotSupportedException("当前 SQL Provider 未实现 Lambda 多源 From 能力。");

    /// <summary>
    /// 获取 Join 子句。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>Lambda 多源 Join 能力。</returns>
    internal static ISqlMultiSourceJoinClause GetJoinClause(ISqlQueryClauseAccessor accessor) =>
        accessor?.JoinClause as ISqlMultiSourceJoinClause ??
        throw new NotSupportedException("当前 SQL Provider 未实现 Lambda 多源 Join 能力。");

    /// <summary>
    /// 从查询子句访问器获取 Lambda 多源 Select 能力。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>当前查询的 Lambda 多源 Select 子句。</returns>
    internal static ISqlMultiSourceSelectClause GetSelectClause(ISqlQueryClauseAccessor accessor) =>
        accessor?.SelectClause as ISqlMultiSourceSelectClause ??
        throw new NotSupportedException("当前 SQL Provider 未实现 Lambda 多源 Select 能力。");

    /// <summary>
    /// 从查询子句访问器获取 Lambda 多源 Group By 能力。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>当前查询的 Lambda 多源 Group By 子句。</returns>
    internal static ISqlMultiSourceGroupByClause GetGroupByClause(ISqlQueryClauseAccessor accessor) =>
        accessor?.GroupByClause as ISqlMultiSourceGroupByClause ??
        throw new NotSupportedException("当前 SQL Provider 未实现 Lambda 多源 Group By 能力。");

    /// <summary>
    /// 从查询子句访问器获取 Lambda 多源 Order By 能力。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>当前查询的 Lambda 多源 Order By 子句。</returns>
    internal static ISqlMultiSourceOrderByClause GetOrderByClause(ISqlQueryClauseAccessor accessor) =>
        accessor?.OrderByClause as ISqlMultiSourceOrderByClause ??
        throw new NotSupportedException("当前 SQL Provider 未实现 Lambda 多源 Order By 能力。");

    /// <summary>
    /// 获取当前查询已绑定的全部表源。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>按查询来源顺序排列的表源集合。</returns>
    internal static IReadOnlyList<TableSource> GetBoundSources(ISqlQueryClauseAccessor accessor)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        var sources = new List<TableSource>(GetFromClause(accessor).Sources);
        sources.AddRange(GetJoinClause(accessor).TypedSources);
        return sources;
    }
}

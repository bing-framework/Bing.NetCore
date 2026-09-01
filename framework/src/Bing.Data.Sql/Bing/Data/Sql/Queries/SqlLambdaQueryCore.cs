using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// Lambda 查询描述的内部执行核心。
/// </summary>
internal sealed class SqlLambdaQueryCore : ISqlQueryBuilderAccessor
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
    /// 基于已有查询对象初始化一个查询核心副本。
    /// </summary>
    /// <param name="query">已有查询对象。</param>
    private SqlLambdaQueryCore(SqlQuery query) => _query = query ?? throw new ArgumentNullException(nameof(query));

    /// <summary>
    /// 克隆当前查询核心及其查询状态。
    /// </summary>
    /// <returns>拥有独立查询状态的查询核心。</returns>
    internal SqlLambdaQueryCore Clone() => new(_query.Clone());

    /// <summary>
    /// 获取当前查询使用的 SQL 生成器。
    /// </summary>
    /// <returns>当前查询的 SQL 生成器。</returns>
    internal ISqlBuilder GetBuilder() => _query.GetBuilder();

    /// <summary>
    /// 获取当前查询计划执行器。
    /// </summary>
    internal ISqlQueryPlanExecutor Executor => _query.Executor;

    /// <summary>
    /// 获取当前查询上下文标识。
    /// </summary>
    internal string QueryContextId => _query.QueryContextId;

    /// <summary>生成当前查询的 SQL 文本。</summary>
    /// <returns>当前查询的 SQL 文本。</returns>
    public string ToSql() => _query.RenderSql();

    /// <summary>
    /// 标记查询结构已成功变更。
    /// </summary>
    internal void Touch() => _query.Touch();

    /// <summary>同步执行当前 Lambda 查询并完整物化指定结果类型。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果列表。</returns>
    public List<TResult> ToList<TResult>(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>
    /// 同步执行当前 Lambda 查询并返回至多一行结果。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的实体；没有结果时返回类型默认值。</returns>
    internal TResult ToEntity<TResult>(int? timeout = null) => _query.ToEntity<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行。</returns>
    public TResult First<TResult>(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询结果的第一行；没有结果时返回类型默认值。</returns>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询到的唯一结果。</returns>
    public TResult Single<TResult>(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的首行首列值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>查询得到的标量值。</returns>
    public TResult Scalar<TResult>(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并返回指定结果类型的分页结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>包含分页数据和分页信息的结果。</returns>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) => _query.ToPage<TResult>(pager, timeout);

    /// <summary>以同步流方式执行当前 Lambda 查询并映射为指定结果类型。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <returns>按需读取查询结果的可枚举序列。</returns>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>异步执行当前 Lambda 查询并完整物化指定结果类型。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果列表的异步任务。</returns>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步执行当前 Lambda 查询并返回至多一行结果。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询实体的异步任务；没有结果时任务结果为类型默认值。</returns>
    internal Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToEntityAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的第一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务。</returns>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询结果第一行的异步任务；没有结果时任务结果为类型默认值。</returns>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的唯一一行。</summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询唯一结果的异步任务。</returns>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的首行首列值。</summary>
    /// <typeparam name="TResult">标量结果类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含查询标量值的异步任务。</returns>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并返回指定结果类型的分页结果。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="pager">分页参数。</param>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分页数据和分页信息的异步任务。</returns>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>以异步流方式执行当前 Lambda 查询并映射为指定结果类型。</summary>
    /// <typeparam name="TResult">结果元素类型。</typeparam>
    /// <param name="timeout">执行超时时间，单位为秒。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>按需读取查询结果的异步可枚举序列。</returns>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    /// <inheritdoc />
    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => GetBuilder();

    /// <inheritdoc />
    void ISqlQueryBuilderAccessor.MarkChanged() => Touch();

    /// <summary>使用已绑定表源解析多表谓词并追加到 Where 子句。</summary>
    /// <param name="expression">条件表达式。</param>
    /// <returns>追加条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore WhereCore(LambdaExpression expression)
    {
        return WhereCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源解析谓词并追加到 Where 子句。</summary>
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
        candidate.ClearWhere().ClearSqlParams();
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
        ((ISqlQueryClauseAccessor)sourceBuilder).WhereClause.And(candidate);
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
        var available = GetBoundSources(accessor).ToList();
        if (aliases != null && aliases.Count != expression.Parameters.Count)
            throw new ArgumentException("条件组来源别名数量必须与表达式参数数量一致。", nameof(aliases));
        var selected = new List<TableSource>(expression.Parameters.Count);
        for (var index = 0; index < expression.Parameters.Count; index++)
        {
            var parameter = expression.Parameters[index];
            var alias = aliases?[index];
            var candidates = available.Where(item => item.EntityType == parameter.Type &&
                selected.Contains(item) == false);
            if (string.IsNullOrWhiteSpace(alias) == false)
                candidates = candidates.Where(item => string.Equals(item.Alias, alias,
                    StringComparison.OrdinalIgnoreCase));
            var matchingSources = candidates.ToList();
            if (matchingSources.Count == 0 && string.IsNullOrWhiteSpace(alias))
                throw new InvalidOperationException($"未找到表达式参数 {parameter.Name} 对应的查询来源。");
            if (matchingSources.Count != 1)
                throw new InvalidOperationException($"实体 {parameter.Type.Name} 的查询来源不唯一，请提供有效别名。");
            var source = matchingSources[0];
            selected.Add(source);
        }
        return selected;
    }

    /// <summary>使用已绑定表源设置多表投影列。</summary>
    /// <param name="expression">投影表达式。</param>
    /// <returns>设置投影后的当前查询核心。</returns>
    internal SqlLambdaQueryCore SelectCore(LambdaExpression expression)
    {
        return SelectCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源设置投影列。</summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>设置投影后的当前查询核心。</returns>
    internal SqlLambdaQueryCore SelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources);
        ReplaceSelect(string.Join(", ", columns));
        Touch();
        return this;
    }

    /// <summary>追加指定来源的投影列。</summary>
    /// <param name="expression">投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void AppendSelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources);
        (accessor.SelectClause as SelectClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表投影查询。"))
            .Select(string.Join(", ", columns));
        Touch();
    }

    /// <summary>
    /// 追加单来源 DTO 投影列。
    /// </summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void AppendSelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = expression.Body is MemberInitExpression
            ? GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, sources)
            : GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources);
        (accessor.SelectClause as SelectClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表投影查询。"))
            .Select(string.Join(", ", columns));
        Touch();
    }

    /// <summary>使用 DTO 成员初始化表达式设置多表投影列。</summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    internal void SelectTypedCore(LambdaExpression expression)
    {
        SelectTypedCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源设置 DTO 投影。</summary>
    /// <param name="expression">DTO 成员初始化或投影表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    internal void SelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = expression.Body is MemberInitExpression
            ? GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, sources)
            : GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources);
        ReplaceSelect(string.Join(", ", columns));
        Touch();
    }

    /// <summary>使用严格 DTO 成员初始化投影创建冻结的类型化派生表。</summary>
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

    /// <summary>使用已绑定表源设置多表分组列。</summary>
    /// <param name="expression">分组表达式。</param>
    /// <returns>追加分组列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore GroupByCore(LambdaExpression expression)
    {
        return GroupByCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源设置分组列。</summary>
    /// <param name="expression">分组表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加分组列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore GroupByCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources));
        Touch();
        return this;
    }

    /// <summary>使用已绑定表源设置多表 Having 条件。</summary>
    /// <param name="expression">Having 条件表达式。</param>
    /// <returns>追加 Having 条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore HavingCore(LambdaExpression expression)
    {
        return HavingCore(expression, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源设置 Having 条件。</summary>
    /// <param name="expression">Having 条件表达式。</param>
    /// <param name="sources">表达式参数对应的查询表源。</param>
    /// <returns>追加 Having 条件后的当前查询核心。</returns>
    internal SqlLambdaQueryCore HavingCore(LambdaExpression expression, IReadOnlyList<TableSource> sources)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .SetBoundHaving(GetFromClause(accessor).ResolveMultiSourcePredicate(expression, sources));
        Touch();
        return this;
    }

    /// <summary>使用已绑定表源设置多表排序列。</summary>
    /// <param name="expression">排序表达式。</param>
    /// <param name="desc">是否按降序排序。</param>
    /// <returns>追加排序列后的当前查询核心。</returns>
    internal SqlLambdaQueryCore OrderByCore(LambdaExpression expression, bool desc)
    {
        return OrderByCore(expression, desc, GetBoundSources((ISqlQueryClauseAccessor)GetBuilder()));
    }

    /// <summary>使用指定表源设置排序列。</summary>
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
        (accessor.OrderByClause as OrderByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表排序查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, sources), desc);
        Touch();
        return this;
    }

    /// <summary>原子添加类型化内连接表。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void JoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().Join<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>按指定左侧来源将二元连接表达式扩展为当前查询图的参数布局。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void JoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class
    {
        JoinAndTouch(() => GetJoinClause().Join<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), alias, schema));
    }

    /// <summary>原子添加类型化左外连接并扩展二元来源绑定。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void LeftJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), alias, schema));

    /// <summary>原子添加类型化右外连接并扩展二元来源绑定。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void RightJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), alias, schema));

    /// <summary>原子添加类型化全外连接并扩展二元来源绑定。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void FullJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin<TJoin>(GetFromClause(), ExpandJoinPredicate(predicate, leftSource), alias, schema));

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

    /// <summary>原子添加类型化左外连接表。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void LeftJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>原子添加类型化右外连接表。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void RightJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>原子添加类型化全外连接表。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="predicate">连接条件表达式。</param>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void FullJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin<TJoin>(GetFromClause(), predicate, alias, schema));

    /// <summary>原子添加类型化派生表内连接。</summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void JoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().Join(GetFromClause(), subquery, predicate));

    /// <summary>按指定左侧来源添加二元类型化派生表内连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void JoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().Join(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>原子添加类型化派生表左外连接。</summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void LeftJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin(GetFromClause(), subquery, predicate));

    /// <summary>按指定左侧来源添加二元类型化派生表左外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void LeftJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().LeftJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>原子添加类型化派生表右外连接。</summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void RightJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin(GetFromClause(), subquery, predicate));

    /// <summary>按指定左侧来源添加二元类型化派生表右外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void RightJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().RightJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>原子添加类型化派生表全外连接。</summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">连接条件表达式。</param>
    internal void FullJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin(GetFromClause(), subquery, predicate));

    /// <summary>按指定左侧来源添加二元类型化派生表全外连接。</summary>
    /// <typeparam name="TLeft">左侧来源实体类型。</typeparam>
    /// <typeparam name="TProjection">派生表投影类型。</typeparam>
    /// <param name="subquery">待连接的派生表。</param>
    /// <param name="predicate">二元连接条件表达式。</param>
    /// <param name="leftSource">连接条件左侧的已有表源。</param>
    internal void FullJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource)
        where TLeft : class where TProjection : class =>
        JoinAndTouch(() => GetJoinClause().FullJoin(GetFromClause(), subquery, ExpandJoinPredicate(predicate, leftSource)));

    /// <summary>添加类型化交叉连接表。</summary>
    /// <typeparam name="TJoin">连接表实体类型。</typeparam>
    /// <param name="alias">连接表别名。</param>
    /// <param name="schema">连接表架构名。</param>
    internal void CrossJoinCore<TJoin>(string alias, string schema) where TJoin : class =>
        JoinAndTouch(() => GetBuilder().CrossJoin<TJoin>(alias, schema));

    /// <summary>添加类型化交叉连接派生表。</summary>
    /// <typeparam name="TJoin">派生表投影类型。</typeparam>
    /// <param name="subquery">待交叉连接的派生表。</param>
    internal void CrossJoinCore<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class =>
        JoinAndTouch(() => ((JoinClause)((ISqlQueryClauseAccessor)GetBuilder()).JoinClause).CrossJoin(subquery));

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
    /// 获取当前查询的根来源子句。
    /// </summary>
    /// <returns>当前查询的根来源子句。</returns>
    internal FromClause GetFromClause() => GetFromClause((ISqlQueryClauseAccessor)GetBuilder());

    /// <summary>
    /// 获取当前查询的连接子句。
    /// </summary>
    /// <returns>当前查询的连接子句。</returns>
    internal JoinClause GetJoinClause() =>
        ((ISqlQueryClauseAccessor)GetBuilder()).JoinClause as JoinClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表连接查询。");

    /// <summary>
    /// 从查询子句访问器获取根来源子句。
    /// </summary>
    /// <param name="accessor">SQL 查询子句访问器。</param>
    /// <returns>查询的根来源子句。</returns>
    internal static FromClause GetFromClause(ISqlQueryClauseAccessor accessor) => accessor.FromClause as FromClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表根来源查询。");

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
        if (accessor.JoinClause is JoinClause joinClause)
            sources.AddRange(joinClause.GetTypedSources());
        return sources;
    }
}

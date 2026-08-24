using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>
/// 使用方法级泛型表达式构建结构化 SQL 查询。
/// </summary>
/// <remarks>
/// 查询来源按调用顺序追加，表达式参数按来源实例绑定，不依赖来源数量生成公共类型。
/// </remarks>
public class SqlLambdaQuery : ISqlQueryBuilderAccessor
{
    private readonly SqlLambdaQueryCore _core;

    internal SqlLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder)
    {
        _core = new SqlLambdaQueryCore(executor, builder);
    }

    internal SqlLambdaQuery(SqlLambdaQuery query) => _core = query?._core ??
        throw new ArgumentNullException(nameof(query));

    private SqlLambdaQuery(SqlLambdaQueryCore core) => _core = core ?? throw new ArgumentNullException(nameof(core));

    internal ISqlBuilder GetBuilder() => _core.GetBuilder();
    internal string QueryContextId => _core.QueryContextId;

    /// <summary>生成当前查询的 SQL 文本。</summary>
    public string ToSql() => _core.ToSql();

    /// <summary>
    /// 克隆当前查询描述为独立的 Draft 查询。
    /// </summary>
    /// <returns>拥有独立 Builder、参数和查询上下文的查询描述。</returns>
    public SqlLambdaQuery Clone()
    {
        return new SqlLambdaQuery(_core.Clone());
    }

    /// <summary>同步执行当前查询并完整物化指定结果类型。</summary>
    public List<TResult> ToList<TResult>(int? timeout = null) => _core.ToList<TResult>(timeout);

    /// <summary>
    /// 查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    public TResult ToEntity<TResult>(int? timeout = null) => _core.ToEntity<TResult>(timeout);

    /// <summary>
    /// 查询全部结果并按指定键和值构造字典。
    /// </summary>
    public Dictionary<TKey, TValue> ToDictionary<TResult, TKey, TValue>(Func<TResult, TKey> keySelector,
        Func<TResult, TValue> valueSelector, int? timeout = null) =>
        _core.ToDictionary(keySelector, valueSelector, timeout);

    /// <summary>同步执行当前查询并获取指定结果类型的第一行。</summary>
    public TResult First<TResult>(int? timeout = null) => _core.First<TResult>(timeout);

    /// <summary>同步执行当前查询并获取指定结果类型的第一行或默认值。</summary>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _core.FirstOrDefault<TResult>(timeout);

    /// <summary>同步执行当前查询并获取指定结果类型的唯一一行。</summary>
    public TResult Single<TResult>(int? timeout = null) => _core.Single<TResult>(timeout);

    /// <summary>同步执行当前查询并获取指定结果类型的唯一一行或默认值。</summary>
    public TResult SingleOrDefault<TResult>(int? timeout = null) => _core.SingleOrDefault<TResult>(timeout);

    /// <summary>同步执行当前查询并获取指定结果类型的首行首列值。</summary>
    public TResult Scalar<TResult>(int? timeout = null) => _core.Scalar<TResult>(timeout);

    /// <summary>同步执行当前查询并返回指定结果类型的分页结果。</summary>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) =>
        _core.ToPage<TResult>(pager, timeout);

    /// <summary>以同步流方式执行当前查询并映射为指定结果类型。</summary>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _core.AsEnumerable<TResult>(timeout);

    /// <summary>异步执行当前查询并完整物化指定结果类型。</summary>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步查询至多一行，零行返回默认值，多行抛出异常。
    /// </summary>
    public Task<TResult> ToEntityAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToEntityAsync<TResult>(timeout, cancellationToken);

    /// <summary>
    /// 异步查询全部结果并按指定键和值构造字典。
    /// </summary>
    public Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TResult, TKey, TValue>(
        Func<TResult, TKey> keySelector, Func<TResult, TValue> valueSelector, int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToDictionaryAsync(keySelector, valueSelector, timeout,
        cancellationToken);

    /// <summary>异步执行当前查询并获取指定结果类型的第一行。</summary>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _core.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前查询并获取指定结果类型的第一行或默认值。</summary>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前查询并获取指定结果类型的唯一一行。</summary>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _core.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前查询并获取指定结果类型的唯一一行或默认值。</summary>
    public Task<TResult> SingleOrDefaultAsync<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.SingleOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前查询并获取指定结果类型的首行首列值。</summary>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _core.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前查询并返回指定结果类型的分页结果。</summary>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _core.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>以异步流方式执行当前查询并映射为指定结果类型。</summary>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _core.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    private void Touch() => _core.Touch();

    internal void LegacySelect<TEntity>(bool propertyAsAlias) where TEntity : class
    {
        var builder = GetBuilder();
        builder.ClearSelect();
        builder.Select<TEntity>(propertyAsAlias);
        Touch();
    }

    internal void LegacySelect<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
        where TEntity : class
    {
        var builder = GetBuilder();
        builder.ClearSelect();
        builder.Select(columns, propertyAsAlias);
        Touch();
    }

    internal void LegacyAppendSelect<TEntity>(bool propertyAsAlias) where TEntity : class
    {
        GetBuilder().Select<TEntity>(propertyAsAlias);
        Touch();
    }

    internal void LegacyAppendSelect<TEntity>(Expression<Func<TEntity, object[]>> columns, bool propertyAsAlias)
        where TEntity : class
    {
        GetBuilder().Select(columns, propertyAsAlias);
        Touch();
    }

    internal void LegacyGroupBy<TEntity>(Expression<Func<TEntity, object>> column) where TEntity : class
    {
        GetBuilder().GroupBy(column);
        Touch();
    }

    internal void LegacyGroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns) where TEntity : class
    {
        GetBuilder().GroupBy(columns);
        Touch();
    }

    internal void LegacyOrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc) where TEntity : class
    {
        GetBuilder().OrderBy(column, desc);
        Touch();
    }

    private static FromClause GetFromClause(ISqlQueryClauseAccessor accessor) =>
        SqlLambdaQueryCore.GetFromClause(accessor);

    private static IReadOnlyList<TableSource> GetBoundSources(ISqlQueryClauseAccessor accessor) =>
        SqlLambdaQueryCore.GetBoundSources(accessor);

    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => GetBuilder();

    private void ReplaceSelect(string columns) => _core.ReplaceSelect(columns);

    private void WhereCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.WhereCore(expression, sources);

    private void WhereGroupCore(Action<ISqlConditionGroup> configure) => _core.WhereGroupCore(configure);

    private void SelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.SelectCore(expression, sources);

    private void SelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.SelectTypedCore(expression, sources);

    private void AppendSelectCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.AppendSelectCore(expression, sources);

    private void AppendSelectTypedCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.AppendSelectTypedCore(expression, sources);

    private SqlSubquery<TProjection> SelectSubqueryCore<TProjection>(LambdaExpression expression, string alias)
        where TProjection : class => _core.SelectSubqueryCore<TProjection>(expression, alias);

    private void GroupByCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.GroupByCore(expression, sources);

    private void OrderByCore(LambdaExpression expression, bool desc, IReadOnlyList<TableSource> sources) =>
        _core.OrderByCore(expression, desc, sources);

    private void HavingCore(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _core.HavingCore(expression, sources);

    private void JoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.JoinCore<TJoin>(predicate, leftSource, alias, schema);

    private void LeftJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.LeftJoinCore<TJoin>(predicate, leftSource, alias, schema);

    private void RightJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.RightJoinCore<TJoin>(predicate, leftSource, alias, schema);

    private void FullJoinCore<TJoin>(LambdaExpression predicate, TableSource leftSource, string alias, string schema)
        where TJoin : class => _core.FullJoinCore<TJoin>(predicate, leftSource, alias, schema);

    private void JoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.JoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    private void LeftJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.LeftJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    private void RightJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.RightJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    private void FullJoinCore<TLeft, TProjection>(SqlSubquery<TProjection> subquery, LambdaExpression predicate,
        TableSource leftSource) where TLeft : class where TProjection : class =>
        _core.FullJoinCore<TLeft, TProjection>(subquery, predicate, leftSource);

    private void CrossJoinCore<TEntity>(string alias, string schema) where TEntity : class =>
        _core.CrossJoinCore<TEntity>(alias, schema);

    private void CrossJoinCore<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        _core.CrossJoinCore(subquery);

    private void SkipCore(int count) => _core.SkipCore(count);

    private void TakeCore(int count) => _core.TakeCore(count);

    /// <summary>
    /// 追加实体来源。
    /// </summary>
    public SqlLambdaQuery From<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).AppendRoot<TEntity>(alias, schema);
        Touch();
        return this;
    }

    /// <summary>
    /// 追加原始表来源。
    /// </summary>
    public SqlLambdaQuery FromTable(string table, string alias = null, string schema = null)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("表名不能为空。", nameof(table));
        var reference = new SqlTableReference
        {
            TableName = table,
            Schema = schema,
            Alias = alias
        };
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).AppendRoot(reference);
        Touch();
        return this;
    }

    /// <summary>
    /// 追加类型化派生表来源。
    /// </summary>
    public SqlLambdaQuery FromSubquery<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class
    {
        if (subquery == null)
            throw new ArgumentNullException(nameof(subquery));
        GetFromClause((ISqlQueryClauseAccessor)GetBuilder()).From(subquery);
        Touch();
        return this;
    }

    /// <summary>
    /// 设置单来源投影。
    /// </summary>
    public SqlLambdaQuery Select<TEntity>(Expression<Func<TEntity, object[]>> columns)
    {
        SelectCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>
    /// 设置单来源 DTO 投影。
    /// </summary>
    public SqlLambdaQuery Select<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection)
    {
        SelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>
    /// 设置双来源投影。
    /// </summary>
    public SqlLambdaQuery Select<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        SelectCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>
    /// 设置双来源 DTO 投影。
    /// </summary>
    public SqlLambdaQuery Select<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection)
    {
        SelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>使用单来源 DTO 投影创建类型化派生表。</summary>
    public SqlSubquery<TProjection> SelectSubquery<TEntity, TProjection>(
        Expression<Func<TEntity, TProjection>> projection, string alias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias);
    }

    /// <summary>使用双来源 DTO 投影创建类型化派生表。</summary>
    public SqlSubquery<TProjection> SelectSubquery<TFirst, TSecond, TProjection>(
        Expression<Func<TFirst, TSecond, TProjection>> projection, string alias)
        where TProjection : class
    {
        return SelectSubqueryCore<TProjection>(projection, alias);
    }

    /// <summary>
    /// 设置实体默认投影。
    /// </summary>
    public SqlLambdaQuery Select<TEntity>(bool propertyAsAlias = false) where TEntity : class
    {
        GetBuilder().Select<TEntity>(propertyAsAlias);
        Touch();
        return this;
    }

    /// <summary>
    /// 追加单来源投影。
    /// </summary>
    public SqlLambdaQuery AppendSelect<TEntity>(Expression<Func<TEntity, object[]>> columns,
        bool propertyAsAlias = false)
        where TEntity : class
    {
        GetBuilder().Select(columns, propertyAsAlias);
        Touch();
        return this;
    }

    /// <summary>按来源别名追加投影列。</summary>
    public SqlLambdaQuery AppendSelect<TEntity>(Expression<Func<TEntity, object[]>> columns, string alias)
        where TEntity : class
    {
        AppendSelectCore(columns, new[] { ResolveSource<TEntity>(alias) });
        return this;
    }

    /// <summary>
    /// 追加单来源 DTO 投影列。
    /// </summary>
    public SqlLambdaQuery AppendSelect<TEntity, TProjection>(Expression<Func<TEntity, TProjection>> projection)
        where TEntity : class
    {
        AppendSelectTypedCore(projection, ResolveSources(projection));
        return this;
    }

    /// <summary>
    /// 清空当前投影。
    /// </summary>
    public SqlLambdaQuery ClearSelect()
    {
        GetBuilder().ClearSelect();
        Touch();
        return this;
    }

    /// <summary>
    /// 启用投影去重。
    /// </summary>
    public SqlLambdaQuery Distinct()
    {
        ((ISqlQueryClauseAccessor)GetBuilder()).SelectClause.Distinct();
        Touch();
        return this;
    }

    /// <summary>使用单来源属性创建聚合投影。</summary>
    public SqlLambdaQuery Aggregate<TEntity>(SqlAggregateFunction function,
        Expression<Func<TEntity, object>> column, string columnAlias = null, bool distinct = false)
        where TEntity : class
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        ResolveSource<TEntity>(null);
        builder.ReplaceSelect(select => select.Aggregate(function, column, columnAlias, distinct));
        Touch();
        return this;
    }

    /// <summary>
    /// 追加单来源条件。
    /// </summary>
    public SqlLambdaQuery Where<TEntity>(Expression<Func<TEntity, bool>> predicate)
    {
        WhereCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>
    /// 追加双来源条件。
    /// </summary>
    public SqlLambdaQuery Where<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        WhereCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>
    /// 追加单来源参数条件。
    /// </summary>
    public SqlLambdaQuery Where<TEntity, TValue>(Expression<Func<TEntity, TValue>> column, TValue value,
        Operator @operator = Operator.Equal)
        where TEntity : class
    {
        var selector = Expression.Lambda<Func<TEntity, object>>(Expression.Convert(column.Body, typeof(object)),
            column.Parameters);
        ((ISqlQueryClauseAccessor)GetBuilder()).WhereClause.Where(selector, value, @operator);
        Touch();
        return this;
    }

    /// <summary>
    /// 按条件追加单来源条件。
    /// </summary>
    public SqlLambdaQuery WhereIf<TEntity>(Expression<Func<TEntity, bool>> predicate, bool condition)
    {
        if (condition)
            Where(predicate);
        return this;
    }

    /// <summary>
    /// 按条件追加单来源参数条件。
    /// </summary>
    public SqlLambdaQuery WhereIf<TEntity>(Expression<Func<TEntity, object>> column, object value, bool condition,
        Operator @operator = Operator.Equal)
        where TEntity : class
    {
        if (condition)
        {
            GetBuilder().WhereIf(column, value, true, @operator);
            Touch();
        }
        return this;
    }

    /// <summary>
    /// 以嵌套 And/Or 条件组追加过滤条件。
    /// </summary>
    public SqlLambdaQuery WhereGroup(Action<ISqlConditionGroup> configure)
    {
        WhereGroupCore(configure);
        return this;
    }

    /// <summary>
    /// 添加内连接。
    /// </summary>
    public SqlLambdaQuery Join<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string alias = null, string leftAlias = null, string schema = null)
        where TLeft : class where TRight : class
    {
        JoinCore<TRight>(predicate, ResolveSource<TLeft>(leftAlias), alias, schema);
        return this;
    }

    /// <summary>
    /// 添加左外连接。
    /// </summary>
    public SqlLambdaQuery LeftJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string alias = null, string leftAlias = null, string schema = null)
        where TLeft : class where TRight : class
    {
        LeftJoinCore<TRight>(predicate, ResolveSource<TLeft>(leftAlias), alias, schema);
        return this;
    }

    /// <summary>
    /// 添加右外连接。
    /// </summary>
    public SqlLambdaQuery RightJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string alias = null, string leftAlias = null, string schema = null)
        where TLeft : class where TRight : class
    {
        RightJoinCore<TRight>(predicate, ResolveSource<TLeft>(leftAlias), alias, schema);
        return this;
    }

    /// <summary>
    /// 添加全外连接。
    /// </summary>
    public SqlLambdaQuery FullJoin<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> predicate,
        string alias = null, string leftAlias = null, string schema = null)
        where TLeft : class where TRight : class
    {
        FullJoinCore<TRight>(predicate, ResolveSource<TLeft>(leftAlias), alias, schema);
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表内连接。</summary>
    public SqlLambdaQuery Join<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        JoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表左外连接。</summary>
    public SqlLambdaQuery LeftJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        LeftJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表右外连接。</summary>
    public SqlLambdaQuery RightJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        RightJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>按指定左侧来源添加类型化派生表全外连接。</summary>
    public SqlLambdaQuery FullJoin<TLeft, TProjection>(SqlSubquery<TProjection> subquery,
        Expression<Func<TLeft, TProjection, bool>> predicate, string leftAlias = null)
        where TLeft : class where TProjection : class
    {
        FullJoinCore<TLeft, TProjection>(subquery, predicate, ResolveSource<TLeft>(leftAlias));
        return this;
    }

    /// <summary>
    /// 添加交叉连接。
    /// </summary>
    public SqlLambdaQuery CrossJoin<TEntity>(string alias = null, string schema = null) where TEntity : class
    {
        CrossJoinCore<TEntity>(alias, schema);
        return this;
    }

    /// <summary>添加类型化派生表交叉连接。</summary>
    public SqlLambdaQuery CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class
    {
        CrossJoinCore(subquery);
        return this;
    }

    /// <summary>
    /// 设置双来源分组列。
    /// </summary>
    public SqlLambdaQuery GroupBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns)
    {
        GroupByCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>设置单来源分组列。</summary>
    public SqlLambdaQuery GroupBy<TEntity>(Expression<Func<TEntity, object[]>> columns)
    {
        GroupByCore(columns, ResolveSources(columns));
        return this;
    }

    /// <summary>
    /// 设置双来源排序列。
    /// </summary>
    public SqlLambdaQuery OrderBy<TFirst, TSecond>(Expression<Func<TFirst, TSecond, object[]>> columns,
        bool desc = false)
    {
        OrderByCore(columns, desc, ResolveSources(columns));
        return this;
    }

    /// <summary>设置单来源排序列。</summary>
    public SqlLambdaQuery OrderBy<TEntity>(Expression<Func<TEntity, object[]>> columns, bool desc = false)
    {
        OrderByCore(columns, desc, ResolveSources(columns));
        return this;
    }

    /// <summary>
    /// 设置双来源 Having 条件。
    /// </summary>
    public SqlLambdaQuery Having<TFirst, TSecond>(Expression<Func<TFirst, TSecond, bool>> predicate)
    {
        HavingCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>设置单来源 Having 条件。</summary>
    public SqlLambdaQuery Having<TEntity>(Expression<Func<TEntity, bool>> predicate)
    {
        HavingCore(predicate, ResolveSources(predicate));
        return this;
    }

    /// <summary>
    /// 跳过指定数量的结果行。
    /// </summary>
    public SqlLambdaQuery Skip(int count)
    {
        SkipCore(count);
        return this;
    }

    /// <summary>
    /// 限制返回的结果行数量。
    /// </summary>
    public SqlLambdaQuery Take(int count)
    {
        TakeCore(count);
        return this;
    }

    private IReadOnlyList<TableSource> ResolveSources(LambdaExpression expression)
    {
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var available = GetBoundSources(accessor).ToList();
        var selected = new List<TableSource>(expression.Parameters.Count);
        foreach (var parameter in expression.Parameters)
        {
            var source = available.FirstOrDefault(item => item.EntityType == parameter.Type &&
                selected.Contains(item) == false &&
                (string.IsNullOrWhiteSpace(parameter.Name) || string.Equals(item.Alias, parameter.Name,
                    StringComparison.OrdinalIgnoreCase) || available.Count(item2 => item2.EntityType == parameter.Type) == 1));
            if (source == null)
                throw new InvalidOperationException($"未找到表达式参数 {parameter.Name} 对应的查询来源。");
            selected.Add(source);
        }
        return selected;
    }

    private TableSource ResolveSource<TEntity>(string alias) where TEntity : class
    {
        var sources = GetBoundSources((ISqlQueryClauseAccessor)GetBuilder())
            .Where(item => item.EntityType == typeof(TEntity)).ToList();
        if (string.IsNullOrWhiteSpace(alias) == false)
            sources = sources.Where(item => string.Equals(item.Alias, alias, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sources.Count != 1)
            throw new InvalidOperationException($"实体 {typeof(TEntity).Name} 的查询来源不唯一，请提供有效别名。");
        return sources[0];
    }
}

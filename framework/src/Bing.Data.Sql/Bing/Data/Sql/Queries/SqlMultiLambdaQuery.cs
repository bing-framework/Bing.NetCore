using System.Linq.Expressions;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql;

/// <summary>
/// 多表强类型 Lambda 查询描述的公共基类。
/// </summary>
public abstract class SqlMultiLambdaQuery : ISqlQueryBuilderAccessor
{
    private readonly SqlQuery _query;

    internal SqlMultiLambdaQuery(ISqlQueryPlanExecutor executor, ISqlBuilder builder) =>
        _query = new SqlQuery(executor, builder);

    internal ISqlBuilder GetBuilder() => _query.GetBuilder();
    internal ISqlQueryPlanExecutor Executor => _query.Executor;

    /// <summary>生成当前查询的 SQL 文本。</summary>
    public string ToSql() => _query.ToSql();

    /// <summary>同步执行当前 Lambda 查询并完整物化指定结果类型。</summary>
    public List<TResult> ToList<TResult>(int? timeout = null) => _query.ToList<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的第一行。</summary>
    public TResult First<TResult>(int? timeout = null) => _query.First<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。</summary>
    public TResult FirstOrDefault<TResult>(int? timeout = null) => _query.FirstOrDefault<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的唯一一行。</summary>
    public TResult Single<TResult>(int? timeout = null) => _query.Single<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的唯一一行或默认值。</summary>
    public TResult SingleOrDefault<TResult>(int? timeout = null) => _query.SingleOrDefault<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并获取指定结果类型的首行首列值。</summary>
    public TResult Scalar<TResult>(int? timeout = null) => _query.Scalar<TResult>(timeout);

    /// <summary>同步执行当前 Lambda 查询并返回指定结果类型的分页结果。</summary>
    public PagerList<TResult> ToPage<TResult>(IPager pager = null, int? timeout = null) => _query.ToPage<TResult>(pager, timeout);

    /// <summary>以同步流方式执行当前 Lambda 查询并映射为指定结果类型。</summary>
    public IEnumerable<TResult> AsEnumerable<TResult>(int? timeout = null) => _query.AsEnumerable<TResult>(timeout);

    /// <summary>异步执行当前 Lambda 查询并完整物化指定结果类型。</summary>
    public Task<List<TResult>> ToListAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ToListAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的第一行。</summary>
    public Task<TResult> FirstAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的第一行或默认值。</summary>
    public Task<TResult> FirstOrDefaultAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.FirstOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的唯一一行。</summary>
    public Task<TResult> SingleAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.SingleAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的唯一一行或默认值。</summary>
    public Task<TResult> SingleOrDefaultAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.SingleOrDefaultAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并获取指定结果类型的首行首列值。</summary>
    public Task<TResult> ScalarAsync<TResult>(int? timeout = null, CancellationToken cancellationToken = default) =>
        _query.ScalarAsync<TResult>(timeout, cancellationToken);

    /// <summary>异步执行当前 Lambda 查询并返回指定结果类型的分页结果。</summary>
    public Task<PagerList<TResult>> ToPageAsync<TResult>(IPager pager = null, int? timeout = null,
        CancellationToken cancellationToken = default) => _query.ToPageAsync<TResult>(pager, timeout, cancellationToken);

    /// <summary>以异步流方式执行当前 Lambda 查询并映射为指定结果类型。</summary>
    public IAsyncEnumerable<TResult> AsAsyncEnumerable<TResult>(int? timeout = null,
        CancellationToken cancellationToken = default) => _query.AsAsyncEnumerable<TResult>(timeout, cancellationToken);

    ISqlBuilder ISqlQueryBuilderAccessor.GetSqlBuilder() => GetBuilder();

    /// <summary>使用已绑定表源解析多表谓词并追加到 Where 子句。</summary>
    protected SqlMultiLambdaQuery WhereCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        accessor.WhereClause.Where(GetFromClause(accessor).ResolveMultiSourcePredicate(expression, GetBoundSources(accessor)));
        return this;
    }

    /// <summary>使用已绑定表源设置多表投影列。</summary>
    protected SqlMultiLambdaQuery SelectCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor));
        ReplaceSelect(string.Join(", ", columns));
        return this;
    }

    /// <summary>使用 DTO 成员初始化表达式设置多表投影列。</summary>
    protected void SelectTypedCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = expression.Body is MemberInitExpression
            ? GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, GetBoundSources(accessor))
            : GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor));
        ReplaceSelect(string.Join(", ", columns));
    }

    /// <summary>使用严格 DTO 成员初始化投影创建冻结的类型化派生表。</summary>
    protected SqlSubquery<TProjection> SelectSubqueryCore<TProjection>(LambdaExpression expression, string alias)
        where TProjection : class
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        var columns = GetFromClause(accessor).ResolveMultiSourceDtoColumns(expression, GetBoundSources(accessor),
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
            context?.MappingProfile, context?.TenantId, sqlBuilder?.GetDatabaseIdentity(), sqlBuilder?.GetExecutionScope());
    }

    /// <summary>使用已绑定表源设置多表分组列。</summary>
    protected SqlMultiLambdaQuery GroupByCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor)));
        return this;
    }

    /// <summary>使用已绑定表源设置多表 Having 条件。</summary>
    protected SqlMultiLambdaQuery HavingCore(LambdaExpression expression)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.GroupByClause as GroupByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表分组查询。"))
            .SetBoundHaving(GetFromClause(accessor).ResolveMultiSourcePredicate(expression, GetBoundSources(accessor)));
        return this;
    }

    /// <summary>使用已绑定表源设置多表排序列。</summary>
    protected SqlMultiLambdaQuery OrderByCore(LambdaExpression expression, bool desc)
    {
        if (expression == null)
            throw new ArgumentNullException(nameof(expression));
        var accessor = (ISqlQueryClauseAccessor)GetBuilder();
        (accessor.OrderByClause as OrderByClause ?? throw new NotSupportedException("当前 SQL Provider 不支持多表排序查询。"))
            .AddBoundColumns(GetFromClause(accessor).ResolveMultiSourceColumns(expression, GetBoundSources(accessor)), desc);
        return this;
    }

    /// <summary>原子添加类型化内连接表。</summary>
    protected void JoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().Join<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>原子添加类型化左外连接表。</summary>
    protected void LeftJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().LeftJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>原子添加类型化右外连接表。</summary>
    protected void RightJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().RightJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>原子添加类型化全外连接表。</summary>
    protected void FullJoinCore<TJoin>(LambdaExpression predicate, string alias, string schema) where TJoin : class =>
        GetJoinClause().FullJoin<TJoin>(GetFromClause(), predicate, alias, schema);

    /// <summary>原子添加类型化派生表内连接。</summary>
    protected void JoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().Join(GetFromClause(), subquery, predicate);

    /// <summary>原子添加类型化派生表左外连接。</summary>
    protected void LeftJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().LeftJoin(GetFromClause(), subquery, predicate);

    /// <summary>原子添加类型化派生表右外连接。</summary>
    protected void RightJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().RightJoin(GetFromClause(), subquery, predicate);

    /// <summary>原子添加类型化派生表全外连接。</summary>
    protected void FullJoinCore<TJoin>(SqlSubquery<TJoin> subquery, LambdaExpression predicate) where TJoin : class =>
        GetJoinClause().FullJoin(GetFromClause(), subquery, predicate);

    /// <summary>添加类型化交叉连接表。</summary>
    protected void CrossJoinCore<TJoin>(string alias, string schema) where TJoin : class =>
        GetBuilder().CrossJoin<TJoin>(alias, schema);

    /// <summary>添加类型化交叉连接派生表。</summary>
    protected void CrossJoinCore<TJoin>(SqlSubquery<TJoin> subquery) where TJoin : class =>
        ((JoinClause)((ISqlQueryClauseAccessor)GetBuilder()).JoinClause).CrossJoin(subquery);

    private void ReplaceSelect(string columns)
    {
        var builder = GetBuilder() as SqlBuilderBase ??
            throw new NotSupportedException("当前 SQL Builder 不支持原子投影替换。");
        builder.ReplaceSelect(select => select.Select(columns));
    }

    protected void SkipCore(int count) => GetBuilder().Skip(count);
    protected void TakeCore(int count) => GetBuilder().Take(count);

    private protected FromClause GetFromClause() => GetFromClause((ISqlQueryClauseAccessor)GetBuilder());

    private protected JoinClause GetJoinClause() =>
        ((ISqlQueryClauseAccessor)GetBuilder()).JoinClause as JoinClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表连接查询。");

    protected static FromClause GetFromClause(ISqlQueryClauseAccessor accessor) => accessor.FromClause as FromClause ??
        throw new NotSupportedException("当前 SQL Provider 不支持多表根来源查询。");

    private protected static IReadOnlyList<TableSource> GetBoundSources(ISqlQueryClauseAccessor accessor)
    {
        if (accessor == null)
            throw new ArgumentNullException(nameof(accessor));
        var sources = new List<TableSource>(GetFromClause(accessor).Sources);
        if (accessor.JoinClause is JoinClause joinClause)
            sources.AddRange(joinClause.GetTypedSources());
        return sources;
    }
}

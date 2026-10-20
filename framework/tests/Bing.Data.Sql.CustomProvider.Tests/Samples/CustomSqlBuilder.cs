using System.Linq.Expressions;
using System.Text;
using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Mutations.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.CustomProvider.Tests.Samples;

/// <summary>
/// 外部 Provider 验收用 SQL Builder。
/// </summary>
internal sealed class CustomSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个 <see cref="CustomSqlBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="services">共享服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    public CustomSqlBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(CustomSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <summary>
    /// 获取共享服务。
    /// </summary>
    public SqlBuilderServices SharedServices => Services;

    /// <inheritdoc />
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new CustomSqlBuilder(Services, parameterManager);
}

/// <summary>
/// 外部 Provider 验收用 SQL Provider。
/// </summary>
internal sealed class CustomSqlProvider : ISqlProvider, ISqlProviderProfileProvider, ISqlMutationClauseFactoryProvider
{
    /// <summary>
    /// 获取 Provider 单例。
    /// </summary>
    public static CustomSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="CustomSqlProvider"/> 类型的实例。
    /// </summary>
    private CustomSqlProvider()
    {
    }

    /// <inheritdoc />
    public string Key => "custom.test";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public IDialect Dialect { get; } = new CustomDialect();

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new CustomClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser { get; } = new CustomTableReferenceParser();

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new CustomPaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } = new ParamLiteralsResolver();

    /// <inheritdoc />
    public ISqlMutationClauseFactory MutationClauseFactory { get; } = new CustomMutationClauseFactory();

    /// <inheritdoc />
    public SqlProviderProfile Profile { get; } = new()
    {
        Query = new SqlProviderQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported },
        Mutation = new SqlProviderMutationCapabilities
        {
            SupportsUpdateFrom = true,
            SupportsDeleteUsing = true,
            SupportsReturning = true
        }
    };
}

/// <summary>
/// 外部 Provider 验收用子句工厂。
/// </summary>
internal sealed class CustomClauseFactory : ISqlClauseFactory
{
    /// <inheritdoc />
    public ISelectClause CreateSelect(SqlClauseContext context) =>
        new CustomSelectClause(new DefaultSqlClauseFactory().CreateSelect(context));

    /// <inheritdoc />
    public IFromClause CreateFrom(SqlClauseContext context) =>
        new CustomFromClause(new DefaultSqlClauseFactory().CreateFrom(context));

    /// <inheritdoc />
    public IJoinClause CreateJoin(SqlClauseContext context) =>
        new CustomJoinClause(new DefaultSqlClauseFactory().CreateJoin(context));

    /// <inheritdoc />
    public IWhereClause CreateWhere(SqlClauseContext context) => new DefaultSqlClauseFactory().CreateWhere(context);

    /// <inheritdoc />
    public IGroupByClause CreateGroupBy(SqlClauseContext context) =>
        new CustomGroupByClause(new DefaultSqlClauseFactory().CreateGroupBy(context));

    /// <inheritdoc />
    public IOrderByClause CreateOrderBy(SqlClauseContext context) =>
        new CustomOrderByClause(new DefaultSqlClauseFactory().CreateOrderBy(context));
}

/// <summary>
/// 外部 Provider 的 Select Clause 实现，仅通过公开 Clause 接口委托默认状态处理。
/// </summary>
internal sealed class CustomSelectClause : ISqlMultiSourceSelectClause
{
    /// <summary>
    /// 内部 Select 子句。
    /// </summary>
    private readonly ISelectClause _inner;

    /// <summary>
    /// 支持多源操作的内部子句。
    /// </summary>
    private readonly ISqlMultiSourceSelectClause _multiSource;

    /// <summary>
    /// 初始化一个 <see cref="CustomSelectClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部 Select 子句。</param>
    public CustomSelectClause(ISelectClause inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _multiSource = inner as ISqlMultiSourceSelectClause ??
            throw new InvalidOperationException("默认 Select Clause 未实现 Lambda 多源 SPI。");
    }

    /// <inheritdoc />
    public bool IsDistinct => _inner.IsDistinct;

    /// <inheritdoc />
    public int? ProjectionCount => _inner.ProjectionCount;

    /// <inheritdoc />
    public void Distinct() => _inner.Distinct();

    /// <inheritdoc />
    public void CountAll(string alias = null) => _inner.CountAll(alias);

    /// <inheritdoc />
    public void CountColumn(string column, string alias = null, bool distinct = false) =>
        _inner.CountColumn(column, alias, distinct);

    /// <inheritdoc />
    public void Count<TEntity>(Expression<Func<TEntity, object>> expression, string alias = null,
        bool distinct = false) where TEntity : class => _inner.Count(expression, alias, distinct);

    /// <inheritdoc />
    public void Aggregate(SqlAggregateFunction function, string column, string columnAlias = null,
        bool distinct = false) => _inner.Aggregate(function, column, columnAlias, distinct);

    /// <inheritdoc />
    public void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string columnAlias = null, bool distinct = false) where TEntity : class =>
        _inner.Aggregate(function, expression, columnAlias, distinct);

    /// <inheritdoc />
    public void AggregateRaw(SqlAggregateFunction function, string argumentSql, string columnAlias = null,
        bool distinct = false) => _inner.AggregateRaw(function, argumentSql, columnAlias, distinct);

    /// <inheritdoc />
    public void AggregateExpression(SqlAggregateFunction function, string expressionSql, string columnAlias = null,
        bool distinct = false) => _inner.AggregateExpression(function, expressionSql, columnAlias, distinct);

    /// <inheritdoc />
    public void Sum(string column, string columnAlias = null, bool distinct = false) =>
        _inner.Sum(column, columnAlias, distinct);

    /// <inheritdoc />
    public void Sum<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class => _inner.Sum(expression, columnAlias, distinct);

    /// <inheritdoc />
    public void Avg(string column, string columnAlias = null, bool distinct = false) =>
        _inner.Avg(column, columnAlias, distinct);

    /// <inheritdoc />
    public void Avg<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class => _inner.Avg(expression, columnAlias, distinct);

    /// <inheritdoc />
    public void Max(string column, string columnAlias = null, bool distinct = false) =>
        _inner.Max(column, columnAlias, distinct);

    /// <inheritdoc />
    public void Max<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class => _inner.Max(expression, columnAlias, distinct);

    /// <inheritdoc />
    public void Min(string column, string columnAlias = null, bool distinct = false) =>
        _inner.Min(column, columnAlias, distinct);

    /// <inheritdoc />
    public void Min<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null,
        bool distinct = false) where TEntity : class => _inner.Min(expression, columnAlias, distinct);

    /// <inheritdoc />
    public void Select(string columns, string tableAlias = null) => _inner.Select(columns, tableAlias);

    /// <inheritdoc />
    public void Select<TEntity>(bool propertyAsAlias = false) =>
        _inner.Select<TEntity>(propertyAsAlias);

    /// <inheritdoc />
    public void Select<TEntity>(Expression<Func<TEntity, object[]>> expression, bool propertyAsAlias = false)
        where TEntity : class => _inner.Select(expression, propertyAsAlias);

    /// <inheritdoc />
    public void Select<TEntity>(Expression<Func<TEntity, object>> expression, string columnAlias = null)
        where TEntity : class => _inner.Select(expression, columnAlias);

    /// <inheritdoc />
    public void Select(ISqlBuilder builder, string columnAlias) => _inner.Select(builder, columnAlias);

    /// <inheritdoc />
    public void Select(Action<ISqlBuilder> action, string columnAlias) => _inner.Select(action, columnAlias);

    /// <inheritdoc />
    public void AppendSql(string sql, string columnAlias = null) => _inner.AppendSql(sql, columnAlias);

    /// <inheritdoc />
    public void RemoveSelect(string columns, string tableAlias = null) => _inner.RemoveSelect(columns, tableAlias);

    /// <inheritdoc />
    public void RemoveSelect<TEntity>(Expression<Func<TEntity, object[]>> expression) where TEntity : class =>
        _inner.RemoveSelect(expression);

    /// <inheritdoc />
    public void RemoveSelect<TEntity>(Expression<Func<TEntity, object>> expression) where TEntity : class =>
        _inner.RemoveSelect(expression);

    /// <inheritdoc />
    public void AppendBoundColumns(string columns) => _multiSource.AppendBoundColumns(columns);

    /// <inheritdoc />
    public void Aggregate<TEntity>(SqlAggregateFunction function, Expression<Func<TEntity, object>> expression,
        string tableAlias, string columnAlias, bool distinct) where TEntity : class =>
        _multiSource.Aggregate(function, expression, tableAlias, columnAlias, distinct);

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public string ToSql() => _inner.ToSql();

    /// <inheritdoc />
    public ISelectClause Clone(SqlClauseContext context) => new CustomSelectClause(_inner.Clone(context));
}

/// <summary>
/// 外部 Provider 的 From Clause 实现，仅通过公开 Clause 接口委托默认状态处理。
/// </summary>
internal sealed class CustomFromClause : ISqlMultiSourceFromClause
{
    /// <summary>
    /// 内部 From 子句。
    /// </summary>
    private readonly IFromClause _inner;

    /// <summary>
    /// 支持多源操作的内部子句。
    /// </summary>
    private readonly ISqlMultiSourceFromClause _multiSource;

    /// <summary>
    /// 初始化一个 <see cref="CustomFromClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部 From 子句。</param>
    public CustomFromClause(IFromClause inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _multiSource = inner as ISqlMultiSourceFromClause ??
            throw new InvalidOperationException("默认 From Clause 未实现 Lambda 多源 SPI。");
    }

    /// <inheritdoc />
    public IReadOnlyList<TableSource> Sources => _multiSource.Sources;

    /// <inheritdoc />
    public void From(string table, string alias = null) => _inner.From(table, alias);

    /// <inheritdoc />
    public void From(SqlTableReference reference) => _inner.From(reference);

    /// <inheritdoc />
    public void From<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.From<TEntity>(alias, schema);

    /// <inheritdoc />
    public void From(ISqlBuilder builder, string alias) => _inner.From(builder, alias);

    /// <inheritdoc />
    public void From(Action<ISqlBuilder> action, string alias) => _inner.From(action, alias);

    /// <inheritdoc />
    public void AppendSql(string sql) => _inner.AppendSql(sql);

    /// <inheritdoc />
    public void Validate() => _inner.Validate();

    /// <inheritdoc />
    public string ToSql() => _inner.ToSql();

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IFromClause Clone(SqlClauseContext context) => new CustomFromClause(_inner.Clone(context));

    /// <inheritdoc />
    public void AppendRoot(Type entityType, string alias = null, string schema = null) =>
        _multiSource.AppendRoot(entityType, alias, schema);

    /// <inheritdoc />
    public void From<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        _multiSource.From(subquery);

    /// <inheritdoc />
    public ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources) =>
        _multiSource.ResolveMultiSourcePredicate(expression, sources);

    /// <inheritdoc />
    public ICondition ResolveMultiSourcePredicate(LambdaExpression expression, IReadOnlyList<TableSource> sources,
        IParameterManager parameterManager) => _multiSource.ResolveMultiSourcePredicate(expression, sources, parameterManager);

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveMultiSourceColumns(LambdaExpression expression,
        IReadOnlyList<TableSource> sources) => _multiSource.ResolveMultiSourceColumns(expression, sources);

    /// <inheritdoc />
    public IReadOnlyList<string> ResolveMultiSourceDtoColumns(LambdaExpression expression,
        IReadOnlyList<TableSource> sources, out IReadOnlyCollection<string> projectedMembers) =>
        _multiSource.ResolveMultiSourceDtoColumns(expression, sources, out projectedMembers);

    /// <inheritdoc />
    public ICondition ResolveMultiSourceValueCondition(LambdaExpression expression, TableSource source, object value,
        Operator @operator) => _multiSource.ResolveMultiSourceValueCondition(expression, source, value, @operator);

    /// <inheritdoc />
    public void MergeNewParameters(IParameterManager parameterManager) => _multiSource.MergeNewParameters(parameterManager);
}

/// <summary>
/// 外部 Provider 的 Group By Clause 实现，仅通过公开 Clause 接口委托默认状态处理。
/// </summary>
internal sealed class CustomGroupByClause : ISqlMultiSourceGroupByClause
{
    /// <summary>
    /// 内部 Group By 子句。
    /// </summary>
    private readonly IGroupByClause _inner;

    /// <summary>
    /// 支持多源操作的内部子句。
    /// </summary>
    private readonly ISqlMultiSourceGroupByClause _multiSource;

    /// <summary>
    /// 初始化一个 <see cref="CustomGroupByClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部 Group By 子句。</param>
    public CustomGroupByClause(IGroupByClause inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _multiSource = inner as ISqlMultiSourceGroupByClause ??
            throw new InvalidOperationException("默认 Group By Clause 未实现 Lambda 多源 SPI。");
    }

    /// <inheritdoc />
    public bool IsGroup => _inner.IsGroup;

    /// <inheritdoc />
    public string GroupColumns => _inner.GroupColumns;

    /// <inheritdoc />
    public void GroupBy(string groupBy) => _inner.GroupBy(groupBy);

    /// <inheritdoc />
    public void GroupBy<TEntity>(params Expression<Func<TEntity, object>>[] columns) =>
        _inner.GroupBy(columns);

    /// <inheritdoc />
    public void GroupBy<TEntity>(Expression<Func<TEntity, object>> column) =>
        _inner.GroupBy(column);

    /// <inheritdoc />
    public void Having(string sql) => _inner.Having(sql);

    /// <inheritdoc />
    public void HavingRaw(string sql) => _inner.HavingRaw(sql);

    /// <inheritdoc />
    public void AppendSql(string sql) => _inner.AppendSql(sql);

    /// <inheritdoc />
    public string ToSql() => _inner.ToSql();

    /// <inheritdoc />
    public void AppendBoundColumns(IEnumerable<string> columns) => _multiSource.AppendBoundColumns(columns);

    /// <inheritdoc />
    public void SetBoundHaving(ICondition condition) => _multiSource.SetBoundHaving(condition);

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IGroupByClause Clone(SqlClauseContext context) => new CustomGroupByClause(_inner.Clone(context));
}

/// <summary>
/// 外部 Provider 的 Order By Clause 实现，仅通过公开 Clause 接口委托默认状态处理。
/// </summary>
internal sealed class CustomOrderByClause : ISqlMultiSourceOrderByClause
{
    /// <summary>
    /// 内部 Order By 子句。
    /// </summary>
    private readonly IOrderByClause _inner;

    /// <summary>
    /// 支持多源操作的内部子句。
    /// </summary>
    private readonly ISqlMultiSourceOrderByClause _multiSource;

    /// <summary>
    /// 初始化一个 <see cref="CustomOrderByClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部 Order By 子句。</param>
    public CustomOrderByClause(IOrderByClause inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _multiSource = inner as ISqlMultiSourceOrderByClause ??
            throw new InvalidOperationException("默认 Order By Clause 未实现 Lambda 多源 SPI。");
    }

    /// <inheritdoc />
    public void OrderBy(string order, string tableAlias = null) => _inner.OrderBy(order, tableAlias);

    /// <inheritdoc />
    public void OrderBy<TEntity>(Expression<Func<TEntity, object>> column, bool desc = false) =>
        _inner.OrderBy(column, desc);

    /// <inheritdoc />
    public void AppendSql(string order) => _inner.AppendSql(order);

    /// <inheritdoc />
    public void Validate(bool isPage) => _inner.Validate(isPage);

    /// <inheritdoc />
    public string ToSql() => _inner.ToSql();

    /// <inheritdoc />
    public void AppendBoundColumns(IEnumerable<string> columns, bool desc) => _multiSource.AppendBoundColumns(columns, desc);

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IOrderByClause Clone(SqlClauseContext context) => new CustomOrderByClause(_inner.Clone(context));
}

/// <summary>
/// 外部 Provider 的 Join Clause 实现，仅通过公开 Clause 接口委托默认状态处理。
/// </summary>
internal sealed class CustomJoinClause : ISqlMultiSourceJoinClause
{
    /// <summary>
    /// 内部 Join 子句。
    /// </summary>
    private readonly IJoinClause _inner;

    /// <summary>
    /// 支持多源操作的内部子句。
    /// </summary>
    private readonly ISqlMultiSourceJoinClause _multiSource;

    /// <summary>
    /// 初始化一个 <see cref="CustomJoinClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">内部 Join 子句。</param>
    public CustomJoinClause(IJoinClause inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _multiSource = inner as ISqlMultiSourceJoinClause ??
            throw new InvalidOperationException("默认 Join Clause 未实现 Lambda 多源 SPI。");
    }

    /// <inheritdoc />
    public IReadOnlyList<TableSource> TypedSources => _multiSource.TypedSources;

    /// <inheritdoc />
    public IJoinOn Find(Type type) => _inner.Find(type);

    /// <inheritdoc />
    public void Join(string table, string alias = null) => _inner.Join(table, alias);

    /// <inheritdoc />
    public void Join(SqlTableReference reference) => _inner.Join(reference);

    /// <inheritdoc />
    public void Join<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.Join<TEntity>(alias, schema);

    /// <inheritdoc />
    public void Join(ISqlBuilder builder, string alias) => _inner.Join(builder, alias);

    /// <inheritdoc />
    public void Join(Action<ISqlBuilder> action, string alias) => _inner.Join(action, alias);

    /// <inheritdoc />
    public void AppendJoin(string sql) => _inner.AppendJoin(sql);

    /// <inheritdoc />
    public void LeftJoin(string table, string alias = null) => _inner.LeftJoin(table, alias);

    /// <inheritdoc />
    public void LeftJoin(SqlTableReference reference) => _inner.LeftJoin(reference);

    /// <inheritdoc />
    public void LeftJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.LeftJoin<TEntity>(alias, schema);

    /// <inheritdoc />
    public void LeftJoin(ISqlBuilder builder, string alias) => _inner.LeftJoin(builder, alias);

    /// <inheritdoc />
    public void LeftJoin(Action<ISqlBuilder> action, string alias) => _inner.LeftJoin(action, alias);

    /// <inheritdoc />
    public void AppendLeftJoin(string sql) => _inner.AppendLeftJoin(sql);

    /// <inheritdoc />
    public void RightJoin(string table, string alias = null) => _inner.RightJoin(table, alias);

    /// <inheritdoc />
    public void RightJoin(SqlTableReference reference) => _inner.RightJoin(reference);

    /// <inheritdoc />
    public void RightJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.RightJoin<TEntity>(alias, schema);

    /// <inheritdoc />
    public void RightJoin(ISqlBuilder builder, string alias) => _inner.RightJoin(builder, alias);

    /// <inheritdoc />
    public void RightJoin(Action<ISqlBuilder> action, string alias) => _inner.RightJoin(action, alias);

    /// <inheritdoc />
    public void AppendRightJoin(string sql) => _inner.AppendRightJoin(sql);

    /// <inheritdoc />
    public void FullJoin(string table, string alias = null) => _inner.FullJoin(table, alias);

    /// <inheritdoc />
    public void FullJoin(SqlTableReference reference) => _inner.FullJoin(reference);

    /// <inheritdoc />
    public void FullJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.FullJoin<TEntity>(alias, schema);

    /// <inheritdoc />
    public void AppendFullJoin(string sql) => _inner.AppendFullJoin(sql);

    /// <inheritdoc />
    public void CrossJoin(string table, string alias = null) => _inner.CrossJoin(table, alias);

    /// <inheritdoc />
    public void CrossJoin(SqlTableReference reference) => _inner.CrossJoin(reference);

    /// <inheritdoc />
    public void CrossJoin<TEntity>(string alias = null, string schema = null) where TEntity : class =>
        _inner.CrossJoin<TEntity>(alias, schema);

    /// <inheritdoc />
    public void AppendCrossJoin(string sql) => _inner.AppendCrossJoin(sql);

    /// <inheritdoc />
    public void On(ICondition condition) => _inner.On(condition);

    /// <inheritdoc />
    public void On(string column, object value, Operator @operator = Operator.Equal) => _inner.On(column, value, @operator);

    /// <inheritdoc />
    public void On<TLeft, TRight>(Expression<Func<TLeft, object>> left, Expression<Func<TRight, object>> right,
        Operator @operator = Operator.Equal) where TLeft : class where TRight : class =>
        _inner.On(left, right, @operator);

    /// <inheritdoc />
    public void On<TLeft, TRight>(Expression<Func<TLeft, TRight, bool>> expression)
        where TLeft : class where TRight : class => _inner.On(expression);

    /// <inheritdoc />
    public void AppendOn(string sql) => _inner.AppendOn(sql);

    /// <inheritdoc />
    public string ToSql() => _inner.ToSql();

    /// <inheritdoc />
    public void AppendTo(StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IJoinClause Clone(SqlClauseContext context) => new CustomJoinClause(_inner.Clone(context));

    /// <inheritdoc />
    public void Join<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => _multiSource.Join<TEntity>(fromClause, predicate, alias, schema);

    /// <inheritdoc />
    public void LeftJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => _multiSource.LeftJoin<TEntity>(fromClause, predicate, alias, schema);

    /// <inheritdoc />
    public void RightJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => _multiSource.RightJoin<TEntity>(fromClause, predicate, alias, schema);

    /// <inheritdoc />
    public void FullJoin<TEntity>(IFromClause fromClause, LambdaExpression predicate, string alias = null,
        string schema = null) where TEntity : class => _multiSource.FullJoin<TEntity>(fromClause, predicate, alias, schema);

    /// <inheritdoc />
    public void Join<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => _multiSource.Join(fromClause, subquery, predicate);

    /// <inheritdoc />
    public void LeftJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => _multiSource.LeftJoin(fromClause, subquery, predicate);

    /// <inheritdoc />
    public void RightJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => _multiSource.RightJoin(fromClause, subquery, predicate);

    /// <inheritdoc />
    public void FullJoin<TProjection>(IFromClause fromClause, SqlSubquery<TProjection> subquery,
        LambdaExpression predicate) where TProjection : class => _multiSource.FullJoin(fromClause, subquery, predicate);

    /// <inheritdoc />
    public void CrossJoin<TProjection>(SqlSubquery<TProjection> subquery) where TProjection : class =>
        _multiSource.CrossJoin(subquery);
}

/// <summary>
/// 外部 Provider 的 Mutation 子句工厂，验证可选方言子句 SPI 的实际分派。
/// </summary>
internal sealed class CustomMutationClauseFactory : ISqlMutationClauseFactory, ISqlUpdateFromClauseFactory,
    ISqlDeleteUsingClauseFactory, ISqlReturningClauseFactory
{
    /// <summary>
    /// 默认 Mutation 子句工厂。
    /// </summary>
    private readonly DefaultSqlMutationClauseFactory _inner = new();

    /// <inheritdoc />
    public IInsertClause CreateInsert(SqlMutationContext context) => _inner.CreateInsert(context);

    /// <inheritdoc />
    public IInsertColumnsClause CreateInsertColumns(SqlMutationContext context) => _inner.CreateInsertColumns(context);

    /// <inheritdoc />
    public IValuesClause CreateValues(SqlMutationContext context) => _inner.CreateValues(context);

    /// <inheritdoc />
    public IUpdateClause CreateUpdate(SqlMutationContext context) => _inner.CreateUpdate(context);

    /// <inheritdoc />
    public IUpdateFromClause CreateUpdateFrom(SqlMutationContext context) => new CustomUpdateFromClause(context);

    /// <inheritdoc />
    public ISetClause CreateSet(SqlMutationContext context) => _inner.CreateSet(context);

    /// <inheritdoc />
    public IDeleteClause CreateDelete(SqlMutationContext context) => _inner.CreateDelete(context);

    /// <inheritdoc />
    public IDeleteUsingClause CreateDeleteUsing(SqlMutationContext context) => new CustomDeleteUsingClause(context);

    /// <inheritdoc />
    public IReturningClause CreateReturning(SqlMutationContext context) => new CustomReturningClause(context);

    /// <inheritdoc />
    public IMutationWhereClause CreateWhere(SqlMutationContext context) => _inner.CreateWhere(context);
}

/// <summary>
/// 外部 Provider 的 Update From 子句代理。
/// </summary>
internal sealed class CustomUpdateFromClause : IUpdateFromClause
{
    /// <summary>
    /// 默认实现。
    /// </summary>
    private readonly IUpdateFromClause _inner;

    /// <summary>
    /// 初始化一个 <see cref="CustomUpdateFromClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public CustomUpdateFromClause(SqlMutationContext context) : this(new UpdateFromClause(context))
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CustomUpdateFromClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">已有的内部 Update From 子句。</param>
    private CustomUpdateFromClause(IUpdateFromClause inner) => _inner = inner;

    /// <inheritdoc />
    public SqlTableReference Table => _inner.Table;

    /// <inheritdoc />
    public void From(SqlTableReference table) => _inner.From(table);

    /// <inheritdoc />
    public void AppendTo(System.Text.StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IUpdateFromClause Clone(SqlMutationContext context) => new CustomUpdateFromClause(_inner.Clone(context));

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => _inner.Validate(context);
}

/// <summary>
/// 外部 Provider 的 Delete Using 子句代理。
/// </summary>
internal sealed class CustomDeleteUsingClause : IDeleteUsingClause
{
    /// <summary>
    /// 默认实现。
    /// </summary>
    private readonly IDeleteUsingClause _inner;

    /// <summary>
    /// 初始化一个 <see cref="CustomDeleteUsingClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public CustomDeleteUsingClause(SqlMutationContext context) : this(new DeleteUsingClause(context))
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CustomDeleteUsingClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">已有的内部 Delete Using 子句。</param>
    private CustomDeleteUsingClause(IDeleteUsingClause inner) => _inner = inner;

    /// <inheritdoc />
    public SqlTableReference Table => _inner.Table;

    /// <inheritdoc />
    public void Using(SqlTableReference table) => _inner.Using(table);

    /// <inheritdoc />
    public void AppendTo(System.Text.StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IDeleteUsingClause Clone(SqlMutationContext context) => new CustomDeleteUsingClause(_inner.Clone(context));

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => _inner.Validate(context);
}

/// <summary>
/// 外部 Provider 的 Returning 子句代理。
/// </summary>
internal sealed class CustomReturningClause : IReturningClause
{
    /// <summary>
    /// 默认实现。
    /// </summary>
    private readonly IReturningClause _inner;

    /// <summary>
    /// 初始化一个 <see cref="CustomReturningClause"/> 类型的实例。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    public CustomReturningClause(SqlMutationContext context) : this(new ReturningClause(context))
    {
    }

    /// <summary>
    /// 初始化一个 <see cref="CustomReturningClause"/> 类型的实例。
    /// </summary>
    /// <param name="inner">已有的内部 Returning 子句。</param>
    private CustomReturningClause(IReturningClause inner) => _inner = inner;

    /// <inheritdoc />
    public bool IsEmpty => _inner.IsEmpty;

    /// <inheritdoc />
    public void AddRange(IReadOnlyList<SqlReturningColumn> columns) => _inner.AddRange(columns);

    /// <inheritdoc />
    public void AppendTo(System.Text.StringBuilder builder) => _inner.AppendTo(builder);

    /// <inheritdoc />
    public void Clear() => _inner.Clear();

    /// <inheritdoc />
    public IReturningClause Clone(SqlMutationContext context) => new CustomReturningClause(_inner.Clone(context));

    /// <inheritdoc />
    public void Validate(SqlValidationContext context) => _inner.Validate(context);
}

/// <summary>
/// 外部 Provider 验收用方言。
/// </summary>
internal sealed class CustomDialect : DialectBase
{
}

/// <summary>
/// 外部 Provider 验收用分页渲染器。
/// </summary>
internal sealed class CustomPaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} Offset {offsetParameterName}";
}

/// <summary>
/// 外部 Provider 验收用表引用解析器。
/// </summary>
internal sealed class CustomTableReferenceParser : ISqlTableReferenceParser
{
    /// <inheritdoc />
    public SqlTableName Parse(string table, string alias = null, string schema = null)
    {
        if (string.Equals(table, "custom:users", StringComparison.OrdinalIgnoreCase))
            return new SqlTableName("ParsedUsers", alias ?? "parsed_users", schema);
        if (string.Equals(table, "custom:orders", StringComparison.OrdinalIgnoreCase))
            return new SqlTableName("ParsedOrders", alias ?? "parsed_orders", schema);
        return DefaultSqlTableReferenceParser.Instance.Parse(table, alias, schema);
    }
}

/// <summary>
/// 外部 Provider 参数数量上限验收用 SQL Builder。
/// </summary>
internal sealed class LimitedCustomSqlBuilder : SqlBuilderBase
{
    /// <summary>
    /// 初始化一个 <see cref="LimitedCustomSqlBuilder"/> 类型的实例。
    /// </summary>
    /// <param name="parameterManager">参数管理器。</param>
    public LimitedCustomSqlBuilder(IParameterManager parameterManager = null)
        : base(LimitedCustomSqlProvider.Instance, SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <inheritdoc />
    protected override SqlBuilderBase CreateBuilder(IParameterManager parameterManager) =>
        new LimitedCustomSqlBuilder(parameterManager);
}

/// <summary>
/// 外部 Provider 参数数量上限验收用 SQL Provider。
/// </summary>
internal sealed class LimitedCustomSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 获取 Provider 单例。
    /// </summary>
    public static LimitedCustomSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="LimitedCustomSqlProvider"/> 类型的实例。
    /// </summary>
    private LimitedCustomSqlProvider()
    {
    }

    /// <inheritdoc />
    public string Key => "custom.limited";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MySql;

    /// <inheritdoc />
    public IDialect Dialect => CustomSqlProvider.Instance.Dialect;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory => CustomSqlProvider.Instance.ClauseFactory;

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => CustomSqlProvider.Instance.TableReferenceParser;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer => CustomSqlProvider.Instance.PaginationRenderer;

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => CustomSqlProvider.Instance.ParameterManagerFactory;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver => CustomSqlProvider.Instance.ParamLiteralsResolver;

    /// <inheritdoc />
    public SqlProviderProfile Profile { get; } = new()
    {
        Limits = new SqlProviderLimits { MaxParameterCount = 1 }
    };
}

/// <summary>
/// 复用 SQLite 数据库类型的外部 Provider 验收用别名。
/// </summary>
internal sealed class CustomSqliteAliasProvider : ISqlProvider
{
    /// <summary>
    /// 获取 Provider 单例。
    /// </summary>
    public static CustomSqliteAliasProvider Instance { get; } = new();

    /// <summary>
    /// 初始化一个 <see cref="CustomSqliteAliasProvider"/> 类型的实例。
    /// </summary>
    private CustomSqliteAliasProvider()
    {
    }

    /// <inheritdoc />
    public string Key => "custom.sqlite-alias";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public IDialect Dialect => CustomSqlProvider.Instance.Dialect;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory => CustomSqlProvider.Instance.ClauseFactory;

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => CustomSqlProvider.Instance.TableReferenceParser;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer => CustomSqlProvider.Instance.PaginationRenderer;

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => CustomSqlProvider.Instance.ParameterManagerFactory;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver => CustomSqlProvider.Instance.ParamLiteralsResolver;
}

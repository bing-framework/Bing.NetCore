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
    /// 初始化外部 Provider SQL Builder。
    /// </summary>
    /// <param name="services">共享服务。</param>
    /// <param name="parameterManager">参数管理器。</param>
    public CustomSqlBuilder(SqlBuilderServices services = null, IParameterManager parameterManager = null)
        : base(CustomSqlProvider.Instance, services ?? SqlBuilderServices.CreateDefault(), parameterManager)
    {
    }

    /// <summary>
    /// 获取共享服务，用于验证 New 与 Clone 的继承边界。
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
    /// Provider 单例。
    /// </summary>
    public static CustomSqlProvider Instance { get; } = new();

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
    public ISelectClause CreateSelect(SqlClauseContext context) => new CustomSelectClause(context);

    /// <inheritdoc />
    public IFromClause CreateFrom(SqlClauseContext context) => new FromClause(context);

    /// <inheritdoc />
    public IJoinClause CreateJoin(SqlClauseContext context) => new JoinClause(context);

    /// <inheritdoc />
    public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

    /// <inheritdoc />
    public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

    /// <inheritdoc />
    public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
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
    /// 初始化外部 Provider Update From 子句。
    /// </summary>
    public CustomUpdateFromClause(SqlMutationContext context) : this(new UpdateFromClause(context))
    {
    }

    /// <summary>
    /// 使用已有内部子句初始化。
    /// </summary>
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
    /// 初始化外部 Provider Delete Using 子句。
    /// </summary>
    public CustomDeleteUsingClause(SqlMutationContext context) : this(new DeleteUsingClause(context))
    {
    }

    /// <summary>
    /// 使用已有内部子句初始化。
    /// </summary>
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
    /// 初始化外部 Provider Returning 子句。
    /// </summary>
    public CustomReturningClause(SqlMutationContext context) : this(new ReturningClause(context))
    {
    }

    /// <summary>
    /// 使用已有内部子句初始化。
    /// </summary>
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
/// 验证 Provider 自定义 Clause 在 Builder 生命周期中保持运行类型的 Select 实现。
/// </summary>
internal sealed class CustomSelectClause : SelectClause
{
    /// <summary>
    /// 初始化自定义 Select 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    public CustomSelectClause(SqlClauseContext context) : base(context)
    {
    }

    /// <summary>
    /// 使用已复制状态初始化自定义 Select 子句。
    /// </summary>
    /// <param name="context">子句运行上下文。</param>
    /// <param name="columns">已复制的列集合。</param>
    /// <param name="distinct">是否保留去重状态。</param>
    private CustomSelectClause(SqlClauseContext context, ColumnCollection columns, bool distinct)
        : base(context, columns, distinct)
    {
    }

    /// <inheritdoc />
    protected override SelectClause CreateClone(SqlClauseContext context, ColumnCollection columns, bool distinct) =>
        new CustomSelectClause(context, columns, distinct);
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
    /// 初始化外部 Provider 参数数量上限验收 Builder。
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
    /// Provider 单例。
    /// </summary>
    public static LimitedCustomSqlProvider Instance { get; } = new();

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
    /// Provider 单例。
    /// </summary>
    public static CustomSqliteAliasProvider Instance { get; } = new();

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
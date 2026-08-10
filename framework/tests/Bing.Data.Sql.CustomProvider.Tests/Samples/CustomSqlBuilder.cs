using Bing.Data.Enums;
using Bing.Data.Sql.Builders;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

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
internal sealed class CustomSqlProvider : ISqlProvider, ISqlProviderProfileProvider
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
    public SqlProviderProfile Profile { get; } = new()
    {
        Query = new SqlProviderQueryCapabilities { Pagination = SqlQueryCapabilityState.Supported }
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
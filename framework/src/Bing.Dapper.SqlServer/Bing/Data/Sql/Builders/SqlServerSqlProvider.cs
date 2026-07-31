using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Builders.Mutations;

namespace Bing.Data.Sql.Builders;

/// <summary>SQL Server SQL 提供程序。</summary>
public sealed class SqlServerSqlProvider : ISqlProvider, ISqlParameterLimitProvider, ISqlProviderCapabilityProvider,
    ISqlReturningDialect
{
    /// <summary>
    /// 可在线程间安全共享的 SQL Server Provider 单例。
    /// </summary>
    public static SqlServerSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化 SQL Server Provider 单例。
    /// </summary>
    private SqlServerSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.sqlserver";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <inheritdoc />
    public IDialect Dialect { get; } = SqlServerDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new SqlServerPaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } =
        global::Bing.Data.Sql.Builders.Params.ParamLiteralsResolver.Instance;

    /// <inheritdoc />
    public SqlProviderCapabilities Capabilities { get; } = new(supportsMultipleResultSets: true,
        supportsMultiRowValues: true, supportsUpdateFrom: false, supportsDeleteUsing: false, supportsReturning: true);

    /// <inheritdoc />
    public SqlReturningClausePosition Position => SqlReturningClausePosition.BeforeSource;

    /// <inheritdoc />
    public string GetKeyword(SqlExecutionKind executionKind) => "Output";

    /// <inheritdoc />
    public string GetQualifier(SqlExecutionKind executionKind, string configuredQualifier) =>
        executionKind == SqlExecutionKind.Delete ? "Deleted" : "Inserted";

    /// <inheritdoc />
    /// <remarks>SQL Server 单个命令最多允许 2100 个参数。</remarks>
    public int? MaxParameterCount => 2100;
}

/// <summary>
/// 渲染 SQL Server <c>Offset ... Fetch Next ...</c> 分页语法。
/// </summary>
internal sealed class SqlServerPaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
}
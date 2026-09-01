using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Mutations;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>SQL Server SQL 提供程序。</summary>
public sealed class SqlServerSqlProvider : ISqlProvider, ISqlReturningDialect,
    ISqlProviderProfileProvider
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
    public SqlProviderProfile Profile { get; } = new()
    {
        Query = new SqlProviderQueryCapabilities
        {
            Cte = SqlQueryCapabilityState.Supported,
            Union = SqlQueryCapabilityState.Supported,
            UnionAll = SqlQueryCapabilityState.Supported,
            Intersect = SqlQueryCapabilityState.Supported,
            Except = SqlQueryCapabilityState.Supported,
            RightJoin = SqlQueryCapabilityState.Supported,
            FullJoin = SqlQueryCapabilityState.Supported
        },
        Mutation = new SqlProviderMutationCapabilities
        {
            SupportsMultiRowValues = true,
            SupportsReturning = true
        },
        Execution = new SqlProviderExecutionCapabilities
        {
            SupportsMultipleResultSets = true,
            SupportsStreaming = true,
            SupportsCancellation = true
        },
        Transaction = new SqlProviderTransactionCapabilities { SupportsTransactions = true },
        Procedure = new SqlProviderProcedureCapabilities
        {
            SupportsStoredProcedures = true,
            SupportsOutputParameters = true
        },
        Limits = new SqlProviderLimits { MaxParameterCount = 2100 }
    };

    /// <inheritdoc />
    public SqlReturningClausePosition Position => SqlReturningClausePosition.BeforeSource;

    /// <inheritdoc />
    public string GetKeyword(SqlExecutionKind executionKind) => "Output";

    /// <inheritdoc />
    public string GetQualifier(SqlExecutionKind executionKind, string configuredQualifier) =>
        executionKind == SqlExecutionKind.Delete ? "Deleted" : "Inserted";

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

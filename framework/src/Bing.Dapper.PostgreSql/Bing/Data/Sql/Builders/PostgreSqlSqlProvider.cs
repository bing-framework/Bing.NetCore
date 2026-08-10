using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>PostgreSQL SQL 提供程序。</summary>
public sealed class PostgreSqlSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 可在线程间安全共享的 PostgreSQL Provider 单例。
    /// </summary>
    public static PostgreSqlSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化 PostgreSQL Provider 单例。
    /// </summary>
    private PostgreSqlSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.postgresql";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.PgSql;

    /// <inheritdoc />
    public IDialect Dialect { get; } = PostgreSqlDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new DefaultSqlClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new PostgreSqlPaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } = PostgreSqlParamLiteralsResolver.Instance;

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
            Pagination = SqlQueryCapabilityState.Supported
        },
        Mutation = new SqlProviderMutationCapabilities
        {
            SupportsMultiRowValues = true,
            SupportsUpdateFrom = true,
            SupportsDeleteUsing = true,
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
        Limits = new SqlProviderLimits { MaxParameterCount = null }
    };
}

/// <summary>
/// 渲染 PostgreSQL <c>Limit ... Offset ...</c> 分页语法。
/// </summary>
internal sealed class PostgreSqlPaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}
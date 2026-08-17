using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>Oracle SQL 提供程序。</summary>
public sealed class OracleSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 可在线程间安全共享的 Oracle Provider 单例。
    /// </summary>
    public static OracleSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化 Oracle Provider 单例。
    /// </summary>
    private OracleSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.oracle";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Oracle;

    /// <inheritdoc />
    public IDialect Dialect { get; } = OracleDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new OracleClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new OraclePaginationRenderer();

    /// <inheritdoc />
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;

    /// <inheritdoc />
    public IParamLiteralsResolver ParamLiteralsResolver { get; } =
        global::Bing.Data.Sql.Builders.Params.ParamLiteralsResolver.Instance;

    /// <inheritdoc />
    /// <remarks>Oracle 不支持标准 <c>Values (...), (...)</c> 多行插入语法。</remarks>
    public SqlProviderProfile Profile { get; } = new()
    {
        Query = new SqlProviderQueryCapabilities
        {
            Cte = SqlQueryCapabilityState.Supported,
            Union = SqlQueryCapabilityState.Supported,
            UnionAll = SqlQueryCapabilityState.Supported,
            Intersect = SqlQueryCapabilityState.Supported,
            Except = SqlQueryCapabilityState.Unsupported,
            RightJoin = SqlQueryCapabilityState.Supported,
            FullJoin = SqlQueryCapabilityState.Supported
        },
        Mutation = new SqlProviderMutationCapabilities { SupportsMultiRowValues = false },
        Execution = new SqlProviderExecutionCapabilities
        {
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

    /// <summary>
    /// 创建使用 Oracle 表引用子句的 Clause 集合。
    /// </summary>
    private sealed class OracleClauseFactory : ISqlClauseFactory
    {
        /// <inheritdoc />
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

        /// <inheritdoc />
        public IFromClause CreateFrom(SqlClauseContext context) => new OracleFromClause(context);

        /// <inheritdoc />
        public IJoinClause CreateJoin(SqlClauseContext context) => new OracleJoinClause(context);

        /// <inheritdoc />
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

        /// <inheritdoc />
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

        /// <inheritdoc />
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

/// <summary>
/// 渲染 Oracle 12c 及更高版本的 <c>Offset ... Fetch Next ...</c> 分页语法。
/// </summary>
internal sealed class OraclePaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
}

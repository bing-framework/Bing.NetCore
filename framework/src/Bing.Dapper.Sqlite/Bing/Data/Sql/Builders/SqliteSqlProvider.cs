using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>SQLite SQL 提供程序。</summary>
public sealed class SqliteSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 可在线程间安全共享的 SQLite Provider 单例。
    /// </summary>
    public static SqliteSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化 SQLite Provider 单例。
    /// </summary>
    private SqliteSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.sqlite";

    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc />
    public IDialect Dialect { get; } = SqliteDialect.Instance;

    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new SqliteClauseFactory();

    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;

    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new SqlitePaginationRenderer();

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
            RightJoin = SqlQueryCapabilityState.Unsupported,
            FullJoin = SqlQueryCapabilityState.Unsupported,
            Pagination = SqlQueryCapabilityState.Supported
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
            SupportsStoredProcedures = false,
            SupportsOutputParameters = false
        },
        Limits = new SqlProviderLimits { MaxParameterCount = null }
    };

    /// <summary>
    /// 创建使用 SQLite 表引用子句的 Clause 集合。
    /// </summary>
    private sealed class SqliteClauseFactory : ISqlClauseFactory
    {
        /// <inheritdoc />
        /// <returns>SQLite Select 子句实例。</returns>
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

        /// <inheritdoc />
        /// <returns>SQLite From 子句实例。</returns>
        public IFromClause CreateFrom(SqlClauseContext context) => new SqliteFromClause(context);

        /// <inheritdoc />
        /// <returns>SQLite Join 子句实例。</returns>
        public IJoinClause CreateJoin(SqlClauseContext context) => new SqliteJoinClause(context);

        /// <inheritdoc />
        /// <returns>SQLite Where 子句实例。</returns>
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

        /// <inheritdoc />
        /// <returns>SQLite Group By 子句实例。</returns>
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

        /// <inheritdoc />
        /// <returns>SQLite Order By 子句实例。</returns>
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

/// <summary>
/// 渲染 SQLite <c>Limit ... Offset ...</c> 分页语法。
/// </summary>
internal sealed class SqlitePaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    /// <returns>SQLite Limit/Offset 分页 SQL 片段。</returns>
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}

using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySQL SQL 提供程序。
/// </summary>
public sealed class MySqlSqlProvider : ISqlProvider, ISqlProviderProfileProvider
{
    /// <summary>
    /// 可在线程间安全共享的 MySQL Provider 单例。
    /// </summary>
    public static MySqlSqlProvider Instance { get; } = new();

    /// <summary>
    /// 初始化 MySQL Provider 单例。
    /// </summary>
    private MySqlSqlProvider() { }

    /// <inheritdoc />
    public string Key => "bing.mysql";
    /// <inheritdoc />
    public DatabaseType DatabaseType => DatabaseType.MySql;
    /// <inheritdoc />
    public IDialect Dialect { get; } = MySqlDialect.Instance;
    /// <inheritdoc />
    public ISqlClauseFactory ClauseFactory { get; } = new MySqlClauseFactory();
    /// <inheritdoc />
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    /// <inheritdoc />
    public ISqlPaginationRenderer PaginationRenderer { get; } = new LimitOffsetPaginationRenderer();
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
            Union = SqlQueryCapabilityState.Supported,
            UnionAll = SqlQueryCapabilityState.Supported,
            RightJoin = SqlQueryCapabilityState.Supported,
            FullJoin = SqlQueryCapabilityState.Unsupported,
            Pagination = SqlQueryCapabilityState.Supported
        },
        Mutation = new SqlProviderMutationCapabilities
        {
            SupportsMultiRowValues = true,
            SupportsUpdateFrom = false,
            SupportsDeleteUsing = false,
            SupportsReturning = false
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

    /// <summary>
    /// 创建使用 MySQL 表引用子句的 Clause 集合。
    /// </summary>
    private sealed class MySqlClauseFactory : ISqlClauseFactory
    {
        /// <inheritdoc />
        /// <returns>MySQL Select 子句实例。</returns>
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

        /// <inheritdoc />
        /// <returns>MySQL From 子句实例。</returns>
        public IFromClause CreateFrom(SqlClauseContext context) => new MySqlFromClause(context);

        /// <inheritdoc />
        /// <returns>MySQL Join 子句实例。</returns>
        public IJoinClause CreateJoin(SqlClauseContext context) => new MySqlJoinClause(context);

        /// <inheritdoc />
        /// <returns>MySQL Where 子句实例。</returns>
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

        /// <inheritdoc />
        /// <returns>MySQL Group By 子句实例。</returns>
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

        /// <inheritdoc />
        /// <returns>MySQL Order By 子句实例。</returns>
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

/// <summary>
/// 渲染 MySQL <c>Limit ... Offset ...</c> 分页语法。
/// </summary>
internal sealed class LimitOffsetPaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    /// <returns>MySQL Limit/Offset 分页 SQL 片段。</returns>
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}

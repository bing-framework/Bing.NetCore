using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>SQLite SQL 提供程序。</summary>
public sealed class SqliteSqlProvider : ISqlProvider, ISqlParameterLimitProvider, ISqlProviderCapabilityProvider
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
    public SqlProviderCapabilities Capabilities { get; } = new(supportsMultipleResultSets: true);

    /// <inheritdoc />
    /// <remarks>当前驱动与版本组合未提供可跨环境保证的固定参数数量上限。</remarks>
    public int? MaxParameterCount => null;

    /// <summary>
    /// 创建使用 SQLite 表引用子句的 Clause 集合。
    /// </summary>
    private sealed class SqliteClauseFactory : ISqlClauseFactory
    {
        /// <inheritdoc />
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

        /// <inheritdoc />
        public IFromClause CreateFrom(SqlClauseContext context) => new SqliteFromClause(context);

        /// <inheritdoc />
        public IJoinClause CreateJoin(SqlClauseContext context) => new SqliteJoinClause(context);

        /// <inheritdoc />
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

        /// <inheritdoc />
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

        /// <inheritdoc />
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

/// <summary>
/// 渲染 SQLite <c>Limit ... Offset ...</c> 分页语法。
/// </summary>
internal sealed class SqlitePaginationRenderer : ISqlPaginationRenderer
{
    /// <inheritdoc />
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}
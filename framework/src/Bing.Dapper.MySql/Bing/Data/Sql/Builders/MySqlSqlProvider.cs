using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// MySQL SQL 提供程序。
/// </summary>
public sealed class MySqlSqlProvider : ISqlProvider, ISqlParameterLimitProvider
{
    /// <summary>默认实例。</summary>
    public static MySqlSqlProvider Instance { get; } = new();
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
    public int? MaxParameterCount => null;

    private sealed class MySqlClauseFactory : ISqlClauseFactory
    {
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);
        public IFromClause CreateFrom(SqlClauseContext context) => new MySqlFromClause(context);
        public IJoinClause CreateJoin(SqlClauseContext context) => new MySqlJoinClause(context);
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

internal sealed class LimitOffsetPaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}
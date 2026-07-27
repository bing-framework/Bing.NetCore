using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>Oracle SQL 提供程序。</summary>
public sealed class OracleSqlProvider : ISqlProvider, ISqlParameterLimitProvider
{
    public static OracleSqlProvider Instance { get; } = new();
    private OracleSqlProvider() { }
    /// <inheritdoc />
    public string Key => "bing.oracle";
    public DatabaseType DatabaseType => DatabaseType.Oracle;
    public IDialect Dialect { get; } = OracleDialect.Instance;
    public ISqlClauseFactory ClauseFactory { get; } = new OracleClauseFactory();
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    public ISqlPaginationRenderer PaginationRenderer { get; } = new OraclePaginationRenderer();
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
    public IParamLiteralsResolver ParamLiteralsResolver { get; } =
        global::Bing.Data.Sql.Builders.Params.ParamLiteralsResolver.Instance;
    /// <inheritdoc />
    public int? MaxParameterCount => null;

    private sealed class OracleClauseFactory : ISqlClauseFactory
    {
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);
        public IFromClause CreateFrom(SqlClauseContext context) => new OracleFromClause(context);
        public IJoinClause CreateJoin(SqlClauseContext context) => new OracleJoinClause(context);
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

internal sealed class OraclePaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Offset {offsetParameterName} Rows Fetch Next {limitParameterName} Rows Only";
}
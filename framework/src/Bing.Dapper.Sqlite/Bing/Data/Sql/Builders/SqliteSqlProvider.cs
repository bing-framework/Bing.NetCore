using Bing.Data.Enums;
using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Core;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders;

/// <summary>SQLite SQL 提供程序。</summary>
public sealed class SqliteSqlProvider : ISqlProvider
{
    public static SqliteSqlProvider Instance { get; } = new();
    private SqliteSqlProvider() { }
    public DatabaseType DatabaseType => DatabaseType.Sqlite;
    public IDialect Dialect => SqliteDialect.Instance;
    public ISqlClauseFactory ClauseFactory { get; } = new SqliteClauseFactory();
    public ISqlTableReferenceParser TableReferenceParser => DefaultSqlTableReferenceParser.Instance;
    public ISqlPaginationRenderer PaginationRenderer { get; } = new SqlitePaginationRenderer();
    public IParameterManagerFactory ParameterManagerFactory => DefaultParameterManagerFactory.Instance;
    public IParamLiteralsResolver ParamLiteralsResolver => new ParamLiteralsResolver();

    private sealed class SqliteClauseFactory : ISqlClauseFactory
    {
        public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);
        public IFromClause CreateFrom(SqlClauseContext context) => new SqliteFromClause(context);
        public IJoinClause CreateJoin(SqlClauseContext context) => new SqliteJoinClause(context);
        public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);
        public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);
        public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
    }
}

internal sealed class SqlitePaginationRenderer : ISqlPaginationRenderer
{
    public string Render(string offsetParameterName, string limitParameterName) =>
        $"Limit {limitParameterName} OFFSET {offsetParameterName}";
}
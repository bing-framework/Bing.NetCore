using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders.Core;

/// <summary>
/// 默认 SQL 子句工厂。
/// </summary>
public sealed class DefaultSqlClauseFactory : ISqlClauseFactory
{
    /// <inheritdoc />
    public ISelectClause CreateSelect(SqlClauseContext context) => new SelectClause(context);

    /// <inheritdoc />
    public IFromClause CreateFrom(SqlClauseContext context) => new FromClause(context);

    /// <inheritdoc />
    public IJoinClause CreateJoin(SqlClauseContext context) => new JoinClause(context);

    /// <inheritdoc />
    public IWhereClause CreateWhere(SqlClauseContext context) => new WhereClause(context);

    /// <inheritdoc />
    public IGroupByClause CreateGroupBy(SqlClauseContext context) => new GroupByClause(context);

    /// <inheritdoc />
    public IOrderByClause CreateOrderBy(SqlClauseContext context) => new OrderByClause(context);
}
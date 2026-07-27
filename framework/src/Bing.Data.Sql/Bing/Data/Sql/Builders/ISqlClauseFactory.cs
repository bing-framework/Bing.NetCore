using Bing.Data.Sql.Builders.Core;

namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 子句工厂。
/// </summary>
public interface ISqlClauseFactory
{
    ISelectClause CreateSelect(SqlClauseContext context);
    IFromClause CreateFrom(SqlClauseContext context);
    IJoinClause CreateJoin(SqlClauseContext context);
    IWhereClause CreateWhere(SqlClauseContext context);
    IGroupByClause CreateGroupBy(SqlClauseContext context);
    IOrderByClause CreateOrderBy(SqlClauseContext context);
}
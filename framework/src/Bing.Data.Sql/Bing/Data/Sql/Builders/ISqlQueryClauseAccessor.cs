namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 查询 Clause 访问器。
/// </summary>
public interface ISqlQueryClauseAccessor
{
    /// <summary>
    /// Select 子句。
    /// </summary>
    ISelectClause SelectClause { get; }

    /// <summary>
    /// From 子句。
    /// </summary>
    IFromClause FromClause { get; }

    /// <summary>
    /// Join 子句。
    /// </summary>
    IJoinClause JoinClause { get; }

    /// <summary>
    /// Where 子句。
    /// </summary>
    IWhereClause WhereClause { get; }

    /// <summary>
    /// GroupBy 子句。
    /// </summary>
    IGroupByClause GroupByClause { get; }

    /// <summary>
    /// OrderBy 子句。
    /// </summary>
    IOrderByClause OrderByClause { get; }
}
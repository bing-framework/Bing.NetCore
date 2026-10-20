namespace Bing.Data.Sql.Builders;

/// <summary>
/// SQL 查询 Clause 访问器。
/// </summary>
public interface ISqlQueryClauseAccessor
{
    /// <summary>
    /// 获取Select 子句。
    /// </summary>
    ISelectClause SelectClause { get; }

    /// <summary>
    /// 获取From 子句。
    /// </summary>
    IFromClause FromClause { get; }

    /// <summary>
    /// 获取Join 子句。
    /// </summary>
    IJoinClause JoinClause { get; }

    /// <summary>
    /// 获取Where 子句。
    /// </summary>
    IWhereClause WhereClause { get; }

    /// <summary>
    /// 获取GroupBy 子句。
    /// </summary>
    IGroupByClause GroupByClause { get; }

    /// <summary>
    /// 获取OrderBy 子句。
    /// </summary>
    IOrderByClause OrderByClause { get; }
}
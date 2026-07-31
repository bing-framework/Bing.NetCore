namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Delete Using 子句访问能力。
/// </summary>
public interface IDeleteUsingClauseAccessor
{
    /// <summary>
    /// Delete Using 来源子句。
    /// </summary>
    IDeleteUsingClause DeleteUsingClause { get; }
}
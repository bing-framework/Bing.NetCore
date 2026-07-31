namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Update From 子句访问能力。
/// </summary>
public interface IUpdateFromClauseAccessor
{
    /// <summary>
    /// Update From 来源子句。
    /// </summary>
    IUpdateFromClause UpdateFromClause { get; }
}

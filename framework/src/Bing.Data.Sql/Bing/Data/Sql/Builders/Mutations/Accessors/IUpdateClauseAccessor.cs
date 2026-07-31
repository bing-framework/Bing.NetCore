namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Update 子句访问能力。
/// </summary>
public interface IUpdateClauseAccessor : IMutationWhereClauseAccessor, IUpdateFromClauseAccessor
{
    /// <summary>
    /// Update 目标表子句。
    /// </summary>
    IUpdateClause UpdateClause { get; }

    /// <summary>
    /// Update Set 子句。
    /// </summary>
    ISetClause SetClause { get; }
}
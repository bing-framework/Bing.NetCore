namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Delete 子句访问能力。
/// </summary>
public interface IDeleteClauseAccessor : IMutationWhereClauseAccessor
{
    /// <summary>
    /// Delete 目标表子句。
    /// </summary>
    IDeleteClause DeleteClause { get; }
}
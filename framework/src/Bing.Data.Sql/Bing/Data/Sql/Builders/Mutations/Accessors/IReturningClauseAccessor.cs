namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Mutation Returning 子句访问能力。
/// </summary>
public interface IReturningClauseAccessor
{
    /// <summary>
    /// Mutation Returning 子句。
    /// </summary>
    IReturningClause ReturningClause { get; }
}
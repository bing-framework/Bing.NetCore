namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Mutation Where 子句访问能力。
/// </summary>
public interface IMutationWhereClauseAccessor
{
    /// <summary>
    /// Mutation Where 子句。
    /// </summary>
    IMutationWhereClause WhereClause { get; }
}
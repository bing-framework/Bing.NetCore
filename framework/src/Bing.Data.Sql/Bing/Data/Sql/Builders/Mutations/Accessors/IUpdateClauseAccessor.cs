namespace Bing.Data.Sql.Builders.Mutations.Accessors;

/// <summary>
/// 提供 Update 子句访问能力。
/// </summary>
public interface IUpdateClauseAccessor : IMutationWhereClauseAccessor, IUpdateFromClauseAccessor
{
    /// <summary>
    /// 获取Update 目标表子句。
    /// </summary>
    IUpdateClause UpdateClause { get; }

    /// <summary>
    /// 获取Update Set 子句。
    /// </summary>
    ISetClause SetClause { get; }
}
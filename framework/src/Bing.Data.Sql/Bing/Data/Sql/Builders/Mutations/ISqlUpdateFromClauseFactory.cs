using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 可选的 Update From 子句工厂。
/// </summary>
public interface ISqlUpdateFromClauseFactory
{
    /// <summary>
    /// 创建 Update From 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Update From 子句。</returns>
    IUpdateFromClause CreateUpdateFrom(SqlMutationContext context);
}

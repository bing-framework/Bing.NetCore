using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 可选的 Returning 子句工厂。
/// </summary>
public interface ISqlReturningClauseFactory
{
    /// <summary>
    /// 创建 Returning 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Returning 子句。</returns>
    IReturningClause CreateReturning(SqlMutationContext context);
}
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 可选的 Delete Using 子句工厂。
/// </summary>
public interface ISqlDeleteUsingClauseFactory
{
    /// <summary>
    /// 创建 Delete Using 子句。
    /// </summary>
    /// <param name="context">Mutation 子句上下文。</param>
    /// <returns>Delete Using 子句。</returns>
    IDeleteUsingClause CreateDeleteUsing(SqlMutationContext context);
}
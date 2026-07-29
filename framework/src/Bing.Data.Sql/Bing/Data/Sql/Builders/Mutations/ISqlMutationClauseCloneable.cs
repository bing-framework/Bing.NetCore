using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 支持使用 Mutation 上下文克隆的 SQL 写操作子句。
/// </summary>
/// <typeparam name="TClause">克隆后的子句类型。</typeparam>
public interface ISqlMutationClauseCloneable<out TClause>
{
    /// <summary>
    /// 使用独立的 Mutation 上下文克隆当前子句。
    /// </summary>
    /// <param name="context">克隆目标的 Mutation 上下文。</param>
    /// <returns>不共享可变状态的子句副本。</returns>
    TClause Clone(SqlMutationContext context);
}
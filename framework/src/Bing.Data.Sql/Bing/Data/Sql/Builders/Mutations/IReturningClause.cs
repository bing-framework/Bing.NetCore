using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Mutation 返回投影子句。
/// </summary>
public interface IReturningClause : ISqlClause, ISqlMutationClauseCloneable<IReturningClause>, ISqlValidatable
{
    /// <summary>
    /// 是否尚未配置返回列。
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// 添加结构化返回列。
    /// </summary>
    /// <param name="columns">已完成边界验证的返回列集合。</param>
    void AddRange(IReadOnlyList<SqlReturningColumn> columns);
}
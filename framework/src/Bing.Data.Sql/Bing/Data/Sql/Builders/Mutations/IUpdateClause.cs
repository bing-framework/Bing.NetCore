using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Update 的目标表子句。
/// </summary>
public interface IUpdateClause : ISqlClause, ISqlMutationClauseCloneable<IUpdateClause>, ISqlValidatable
{
    /// <summary>
    /// 当前更新目标表。
    /// </summary>
    SqlTableReference Table { get; }

    /// <summary>
    /// 设置更新目标表。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    void UpdateTable(SqlTableReference table);
}
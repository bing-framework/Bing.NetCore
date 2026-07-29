using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Delete 的目标表子句。
/// </summary>
public interface IDeleteClause : ISqlClause, ISqlMutationClauseCloneable<IDeleteClause>, ISqlValidatable
{
    /// <summary>
    /// 当前删除目标表。
    /// </summary>
    SqlTableReference Table { get; }

    /// <summary>
    /// 设置删除目标表。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    void From(SqlTableReference table);
}
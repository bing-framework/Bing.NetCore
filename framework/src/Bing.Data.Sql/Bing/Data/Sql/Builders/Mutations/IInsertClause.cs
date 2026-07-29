using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Insert 的目标表子句。
/// </summary>
public interface IInsertClause : ISqlClause, ISqlMutationClauseCloneable<IInsertClause>, ISqlValidatable
{
    /// <summary>
    /// 当前插入目标表。
    /// </summary>
    SqlTableReference Table { get; }

    /// <summary>
    /// 设置插入目标表。
    /// </summary>
    /// <param name="table">结构化表引用。</param>
    void Into(SqlTableReference table);
}
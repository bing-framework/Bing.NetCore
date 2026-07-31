using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Update 的 From 来源子句。
/// </summary>
public interface IUpdateFromClause : ISqlClause, ISqlMutationClauseCloneable<IUpdateFromClause>, ISqlValidatable
{
    /// <summary>
    /// 当前结构化来源表。
    /// </summary>
    SqlTableReference Table { get; }

    /// <summary>
    /// 设置结构化来源表。
    /// </summary>
    /// <param name="table">来源表；必须包含别名。</param>
    void From(SqlTableReference table);
}

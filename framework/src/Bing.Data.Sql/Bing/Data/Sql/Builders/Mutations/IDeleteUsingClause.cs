using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Mutations.Contexts;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Delete 的 Using 来源子句。
/// </summary>
public interface IDeleteUsingClause : ISqlClause, ISqlMutationClauseCloneable<IDeleteUsingClause>, ISqlValidatable
{
    /// <summary>
    /// 当前结构化来源表。
    /// </summary>
    SqlTableReference Table { get; }

    /// <summary>
    /// 设置结构化来源表。
    /// </summary>
    /// <param name="table">来源表；必须包含别名。</param>
    void Using(SqlTableReference table);
}
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Update 与 Delete 共用的筛选子句。
/// </summary>
public interface IMutationWhereClause : ISqlClause, ISqlMutationClauseCloneable<IMutationWhereClause>, ISqlValidatable
{
    /// <summary>
    /// 是否尚未配置筛选条件。
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// 使用 And 连接条件。
    /// </summary>
    /// <param name="condition">查询条件。</param>
    void And(ICondition condition);

    /// <summary>
    /// 使用 Or 连接条件。
    /// </summary>
    /// <param name="condition">查询条件。</param>
    void Or(ICondition condition);
}
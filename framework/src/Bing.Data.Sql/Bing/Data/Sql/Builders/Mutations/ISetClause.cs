using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Update 的 Set 子句。
/// </summary>
public interface ISetClause : ISqlClause, ISqlMutationClauseCloneable<ISetClause>, ISqlValidatable
{
    /// <summary>
    /// 获取当前赋值项数量。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 设置指定列的值。
    /// </summary>
    /// <param name="column">逻辑列名。</param>
    /// <param name="value">列值。</param>
    void Set(string column, object value);

    /// <summary>
    /// 设置指定列的值。
    /// </summary>
    /// <param name="column">逻辑列名。</param>
    /// <param name="parameter">SQL 参数。</param>
    void Set(string column, SqlParam parameter);
}

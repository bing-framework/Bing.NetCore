using Bing.Data.Sql.Builders.Clauses;
using Bing.Data.Sql.Builders.Params;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Insert 的 Values 子句。
/// </summary>
public interface IValuesClause : ISqlClause, ISqlMutationClauseCloneable<IValuesClause>, ISqlValidatable
{
    /// <summary>
    /// 当前 Values 行数。
    /// </summary>
    int RowCount { get; }

    /// <summary>
    /// 当前 Values 列数；没有行时返回零。
    /// </summary>
    int ColumnCount { get; }

    /// <summary>
    /// 添加一行 Values。
    /// </summary>
    /// <param name="values">本行值集合。</param>
    void AddRow(IReadOnlyList<object> values);

    /// <summary>
    /// 添加包含数据库参数元数据的一行 Values。
    /// </summary>
    /// <param name="parameters">本行已分配名称的 SQL 参数。</param>
    void AddRow(IReadOnlyList<SqlParam> parameters);

    /// <summary>
    /// 添加多行 Values。
    /// </summary>
    /// <param name="rows">待添加的 Values 行集合。</param>
    void AddRows(IEnumerable<IReadOnlyList<object>> rows);
}
using Bing.Data.Sql.Builders.Clauses;

namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Insert 的目标列子句。
/// </summary>
public interface IInsertColumnsClause : ISqlClause, ISqlMutationClauseCloneable<IInsertColumnsClause>, ISqlValidatable
{
    /// <summary>
    /// 当前插入列名集合。
    /// </summary>
    IReadOnlyList<string> Columns { get; }

    /// <summary>
    /// 添加一个插入列。
    /// </summary>
    /// <param name="column">逻辑列名。</param>
    void Add(string column);

    /// <summary>
    /// 添加多个插入列。
    /// </summary>
    /// <param name="columns">逻辑列名集合。</param>
    void AddRange(IEnumerable<string> columns);
}
namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// Mutation Returning 的结构化返回列。
/// </summary>
public sealed class SqlReturningColumn
{
    /// <summary>
    /// 初始化一个 <see cref="SqlReturningColumn"/> 类型的实例。
    /// </summary>
    /// <param name="column">物理列名。</param>
    /// <param name="qualifier">可选目标表别名。</param>
    /// <param name="alias">可选结果列别名。</param>
    public SqlReturningColumn(string column, string qualifier = null, string alias = null)
    {
        Column = string.IsNullOrWhiteSpace(column)
            ? throw new ArgumentException("Returning 列名不能为空。", nameof(column))
            : column;
        Qualifier = qualifier;
        Alias = alias;
    }

    /// <summary>
    /// 物理列名。
    /// </summary>
    public string Column { get; }

    /// <summary>
    /// 可选目标表别名。
    /// </summary>
    public string Qualifier { get; }

    /// <summary>
    /// 可选结果列别名。
    /// </summary>
    public string Alias { get; }
}
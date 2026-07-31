namespace Bing.Data.Sql.Builders.Mutations;

/// <summary>
/// 支持使用来源表列设置 Update 值的 Set 子句。
/// </summary>
public interface IColumnSetClause
{
    /// <summary>
    /// 将目标列设置为来源表列。
    /// </summary>
    /// <param name="targetColumn">目标列。</param>
    /// <param name="sourceAlias">来源表别名。</param>
    /// <param name="sourceColumn">来源列。</param>
    void SetFrom(string targetColumn, string sourceAlias, string sourceColumn);
}

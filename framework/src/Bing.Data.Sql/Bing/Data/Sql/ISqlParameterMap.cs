namespace Bing.Data.Sql;

/// <summary>
/// SQL 参数映射。
/// </summary>
public interface ISqlParameterMap
{
    /// <summary>
    /// 参数源对象。
    /// </summary>
    object Source { get; }

    /// <summary>
    /// 获取参数映射项集合。
    /// </summary>
    /// <returns>参数映射项集合。</returns>
    IReadOnlyCollection<SqlParameterMapItem> GetItems();
}
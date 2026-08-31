namespace Bing.Data.Sql.Builders;

/// <summary>
/// Sql查询条件
/// </summary>
public interface ICondition
{
    /// <summary>
    /// 获取查询条件
    /// </summary>
    /// <returns>当前条件的 SQL 片段。</returns>
    string GetCondition();
}
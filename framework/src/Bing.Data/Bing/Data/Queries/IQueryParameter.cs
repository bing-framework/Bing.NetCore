namespace Bing.Data.Queries;

/// <summary>
/// 定义通用查询参数。
/// </summary>
public interface IQueryParameter : IPager
{
    /// <summary>
    /// 获取或设置搜索关键字。
    /// </summary>
    string Keyword { get; set; }
}
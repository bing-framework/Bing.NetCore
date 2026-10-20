namespace Bing.Data;

/// <summary>
/// 分页基类
/// </summary>
public interface IPagerBase
{
    /// <summary>
    /// 获取或设置页数，即第几页，从1开始
    /// </summary>
    int Page { get; set; }

    /// <summary>
    /// 获取或设置每页显示行数
    /// </summary>
    int PageSize { get; set; }

    /// <summary>
    /// 获取或设置总行数
    /// </summary>
    int TotalCount { get; set; }
}
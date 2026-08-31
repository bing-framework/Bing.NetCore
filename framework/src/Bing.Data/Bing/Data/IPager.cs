namespace Bing.Data;

/// <summary>
/// 分页
/// </summary>
public interface IPager : IPagerBase
{
    /// <summary>
    /// 排序条件
    /// </summary>
    string Order { get; set; }

    /// <summary>
    /// 获取总页数
    /// </summary>
    /// <returns>根据总行数和每页行数计算出的总页数。</returns>
    int GetPageCount();

    /// <summary>
    /// 获取跳过的行数
    /// </summary>
    /// <returns>当前页之前需要跳过的行数。</returns>
    int GetSkipCount();

    /// <summary>
    /// 获取起始行数
    /// </summary>
    /// <returns>当前页的起始行号。</returns>
    int GetStartNumber();

    /// <summary>
    /// 获取结束行数
    /// </summary>
    /// <returns>当前页的结束行号。</returns>
    int GetEndNumber();
}
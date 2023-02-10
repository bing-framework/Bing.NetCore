namespace Bing.Authorization.Modules;

/// <summary>
/// 模块信息
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// 代码
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 排序号
    /// </summary>
    public int SortId { get; set; }

    /// <summary>
    /// 位置。父模块Code以点号 . 相连的字符串
    /// </summary>
    public string Position { get; set; }
}

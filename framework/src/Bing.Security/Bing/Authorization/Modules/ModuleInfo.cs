namespace Bing.Authorization.Modules;

/// <summary>
/// 表示权限系统中的模块及其层级位置。
/// </summary>
public class ModuleInfo
{
    /// <summary>
    /// 获取或设置模块的唯一代码。
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// 获取或设置模块的显示名称。
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 获取或设置同级模块的排序号。
    /// </summary>
    public int SortId { get; set; }

    /// <summary>
    /// 获取或设置模块的层级位置；父模块代码按从上到下使用点号拼接。
    /// </summary>
    public string Position { get; set; }
}

namespace Bing.Core.Modularity;

/// <summary>
/// 当前应用已经冻结的模块目录。
/// </summary>
public interface IBingModuleContainer
{
    /// <summary>
    /// 获取按执行顺序排列的只读模块描述符。
    /// </summary>
    IReadOnlyList<BingModuleDescriptor> Modules { get; }
}

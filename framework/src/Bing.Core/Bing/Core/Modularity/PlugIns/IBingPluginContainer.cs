using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 当前应用已经加载的启动期插件目录。
/// </summary>
public interface IBingPluginContainer
{
    /// <summary>
    /// 获取按插件依赖顺序排列的只读描述符。
    /// </summary>
    IReadOnlyList<BingPluginDescriptor> Plugins { get; }
}

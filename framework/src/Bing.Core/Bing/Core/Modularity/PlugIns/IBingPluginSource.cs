using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供启动期插件目录。
/// </summary>
/// <remarks>
/// 插件来源只在应用注册阶段读取；实现不应在调用期间长期持有应用服务。
/// </remarks>
public interface IBingPluginSource
{
    /// <summary>
    /// 获取需要读取清单的插件目录。
    /// </summary>
    /// <returns>插件目录路径序列。</returns>
    IEnumerable<string> GetPluginDirectories();
}

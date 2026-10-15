using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 标识插件代的运行状态。
/// </summary>
public enum BingHotPluginGenerationState
{
    /// <summary>
    /// 正在接收新调用。
    /// </summary>
    Active,

    /// <summary>
    /// 正在等待已开始的调用退出。
    /// </summary>
    Draining,

    /// <summary>
    /// 正在关闭模块和容器。
    /// </summary>
    Closing
}

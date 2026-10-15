using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 标识插件重新加载失败的阶段。
/// </summary>
public enum BingHotPluginReloadPhase
{
    /// <summary>
    /// 等待本宿主的其他重新加载操作。
    /// </summary>
    WaitForTurn,

    /// <summary>
    /// 构建并初始化候选代。
    /// </summary>
    BuildCandidate,

    /// <summary>
    /// 等待旧代的活动调用退出。
    /// </summary>
    WaitForPreviousCalls,

    /// <summary>
    /// 关闭旧代。
    /// </summary>
    ClosePrevious
}

using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供插件代状态快照。
/// </summary>
/// <remarks>快照不持有插件程序集引用。</remarks>
public sealed class BingHotPluginGenerationStatus
{
    /// <summary>
    /// 初始化一个 <see cref="BingHotPluginGenerationStatus" /> 类型的实例。
    /// </summary>
    /// <param name="id">插件代标识。</param>
    /// <param name="plugins">插件信息。</param>
    /// <param name="activeCalls">活动调用数。</param>
    /// <param name="state">插件代的运行状态。</param>
    internal BingHotPluginGenerationStatus(Guid id, IReadOnlyList<BingHotPluginInfo> plugins,
        int activeCalls, BingHotPluginGenerationState state)
    {
        Id = id;
        Plugins = plugins;
        ActiveCalls = activeCalls;
        State = state;
    }

    /// <summary>
    /// 获取插件代标识。
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// 获取插件信息。
    /// </summary>
    public IReadOnlyList<BingHotPluginInfo> Plugins { get; }

    /// <summary>
    /// 获取活动调用数。
    /// </summary>
    public int ActiveCalls { get; }

    /// <summary>
    /// 获取插件代的运行状态。
    /// </summary>
    public BingHotPluginGenerationState State { get; }

    /// <summary>
    /// 获取是否正在退役。
    /// </summary>
    public bool IsRetiring => State != BingHotPluginGenerationState.Active;
}

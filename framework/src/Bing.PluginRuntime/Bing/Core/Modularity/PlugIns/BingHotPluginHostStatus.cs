using System.Collections.ObjectModel;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供插件宿主的代状态快照。
/// </summary>
public sealed class BingHotPluginHostStatus
{
    /// <summary>
    /// 初始化一个 <see cref="BingHotPluginHostStatus" /> 类型的实例。
    /// </summary>
    /// <param name="current">当前可用的插件代。</param>
    /// <param name="retiring">正在等待或关闭的插件代。</param>
    internal BingHotPluginHostStatus(BingHotPluginGenerationStatus current,
        IEnumerable<BingHotPluginGenerationStatus> retiring)
    {
        Current = current;
        Retiring = new ReadOnlyCollection<BingHotPluginGenerationStatus>(retiring.ToArray());
    }

    /// <summary>
    /// 获取当前可用的插件代。
    /// </summary>
    public BingHotPluginGenerationStatus Current { get; }

    /// <summary>
    /// 获取正在退役的插件代。
    /// </summary>
    public IReadOnlyList<BingHotPluginGenerationStatus> Retiring { get; }
}

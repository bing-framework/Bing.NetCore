using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 保存当前应用的插件描述符快照。
/// </summary>
internal sealed class BingPluginContainer : IBingPluginContainer
{
    /// <summary>
    /// 当前应用的插件描述符快照。
    /// </summary>
    private IReadOnlyList<BingPluginDescriptor> _plugins = Array.Empty<BingPluginDescriptor>();

    /// <inheritdoc />
    public IReadOnlyList<BingPluginDescriptor> Plugins => _plugins;

    /// <summary>
    /// 更新插件描述符快照。
    /// </summary>
    /// <param name="plugins">按依赖顺序排列的插件描述符。</param>
    internal void SetPlugins(IEnumerable<BingPluginDescriptor> plugins) =>
        _plugins = Array.AsReadOnly((plugins ?? throw new ArgumentNullException(nameof(plugins))).ToArray());

    /// <summary>
    /// 清空插件描述符快照。
    /// </summary>
    internal void Clear() => _plugins = Array.Empty<BingPluginDescriptor>();
}

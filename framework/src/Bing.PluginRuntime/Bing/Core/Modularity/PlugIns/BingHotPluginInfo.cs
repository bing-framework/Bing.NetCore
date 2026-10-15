using System.Collections.ObjectModel;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Core.Modularity;

/// <summary>
/// 提供当前插件信息。
/// </summary>
/// <remarks>快照不持有插件程序集引用。</remarks>
public sealed class BingHotPluginInfo
{
    /// <summary>
    /// 初始化一个 <see cref="BingHotPluginInfo" /> 类型的实例。
    /// </summary>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件数值版本。</param>
    /// <param name="versionText">插件完整版本。</param>
    internal BingHotPluginInfo(string id, Version version, string versionText)
    {
        Id = id;
        Version = version;
        VersionText = versionText;
    }

    /// <summary>
    /// 获取插件 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取插件数值版本。
    /// </summary>
    /// <remarks>不包含预发布和构建元数据。</remarks>
    public Version Version { get; }

    /// <summary>
    /// 获取插件完整版本。
    /// </summary>
    public string VersionText { get; }
}

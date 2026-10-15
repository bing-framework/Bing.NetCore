using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 插件之间的直接版本依赖描述。
/// </summary>
public sealed class BingPluginDependencyDescriptor
{
    /// <summary>
    /// 初始化一个 <see cref="BingPluginDependencyDescriptor" /> 类型的实例。
    /// </summary>
    /// <param name="id">依赖插件 ID。</param>
    /// <param name="version">精确版本；区间约束时为空。</param>
    /// <param name="versionRequirement">清单声明的版本约束。</param>
    internal BingPluginDependencyDescriptor(string id, Version version, string versionRequirement)
    {
        Id = id;
        Version = version;
        VersionRequirement = versionRequirement;
    }

    /// <summary>
    /// 获取依赖插件 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取精确依赖版本；区间约束时为空。
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// 获取依赖插件的版本约束。
    /// </summary>
    public string VersionRequirement { get; }
}

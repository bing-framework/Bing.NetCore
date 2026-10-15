using System.Collections.ObjectModel;
using System.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 启动期插件的只读描述。
/// </summary>
public sealed class BingPluginDescriptor
{
    /// <summary>
    /// 初始化一个 <see cref="BingPluginDescriptor" /> 类型的实例。
    /// </summary>
    /// <param name="id">插件 ID。</param>
    /// <param name="version">插件数值版本。</param>
    /// <param name="versionText">插件完整版本。</param>
    /// <param name="directoryPath">插件目录。</param>
    /// <param name="entryAssemblyPath">入口程序集路径。</param>
    /// <param name="entryAssembly">已加载的入口程序集。</param>
    /// <param name="startupModules">入口程序集中的启动模块。</param>
    /// <param name="dependencies">插件直接依赖。</param>
    /// <param name="executionOrder">插件加载执行序号。</param>
    internal BingPluginDescriptor(string id, Version version, string versionText, string directoryPath, string entryAssemblyPath,
        Assembly entryAssembly, IEnumerable<Type> startupModules,
        IEnumerable<BingPluginDependencyDescriptor> dependencies, int executionOrder)
    {
        Id = id;
        Version = version;
        VersionText = versionText;
        DirectoryPath = directoryPath;
        EntryAssemblyPath = entryAssemblyPath;
        EntryAssembly = entryAssembly ?? throw new ArgumentNullException(nameof(entryAssembly));
        StartupModules = new ReadOnlyCollection<Type>((startupModules ?? throw new ArgumentNullException(nameof(startupModules))).ToArray());
        Dependencies = new ReadOnlyCollection<BingPluginDependencyDescriptor>(
            (dependencies ?? throw new ArgumentNullException(nameof(dependencies))).ToArray());
        ExecutionOrder = executionOrder;
    }

    /// <summary>
    /// 获取插件 ID。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取插件数值版本，不包含预发布和构建元数据。
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// 获取插件完整版本。
    /// </summary>
    public string VersionText { get; }

    /// <summary>
    /// 获取插件目录的规范化路径。
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// 获取入口程序集的规范化路径。
    /// </summary>
    public string EntryAssemblyPath { get; }

    /// <summary>
    /// 获取已加载的入口程序集。
    /// </summary>
    public Assembly EntryAssembly { get; }

    /// <summary>
    /// 获取插件声明的启动模块类型。
    /// </summary>
    public IReadOnlyList<Type> StartupModules { get; }

    /// <summary>
    /// 获取插件的直接依赖。
    /// </summary>
    public IReadOnlyList<BingPluginDependencyDescriptor> Dependencies { get; }

    /// <summary>
    /// 获取插件在依赖加载序列中的序号。
    /// </summary>
    public int ExecutionOrder { get; }
}

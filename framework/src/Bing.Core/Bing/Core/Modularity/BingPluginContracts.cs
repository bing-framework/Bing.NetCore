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

/// <summary>
/// 管理应用使用的启动期插件来源。
/// </summary>
public sealed class BingPluginSourceList : Collection<IBingPluginSource>
{
    /// <summary>
    /// 添加一个插件目录来源。
    /// </summary>
    /// <param name="directory">包含 <c>bing-plugin.json</c> 的插件目录。</param>
    /// <returns>当前来源集合。</returns>
    public BingPluginSourceList AddDirectory(string directory)
    {
        Add(new DirectoryBingPluginSource(directory, false));
        return this;
    }

    /// <summary>
    /// 添加一个插件父目录来源。
    /// </summary>
    /// <param name="directory">包含插件子目录的父目录。</param>
    /// <returns>当前来源集合。</returns>
    public BingPluginSourceList AddFolder(string directory)
    {
        Add(new DirectoryBingPluginSource(directory, true));
        return this;
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, IBingPluginSource item)
    {
        base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));
    }
}

/// <summary>
/// 插件之间的直接版本依赖描述。
/// </summary>
public sealed class BingPluginDependencyDescriptor
{
    /// <summary>
    /// 初始化插件依赖描述。
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

/// <summary>
/// 启动期插件的只读描述。
/// </summary>
public sealed class BingPluginDescriptor
{
    /// <summary>
    /// 初始化插件描述。
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

/// <summary>
/// 使用文件系统目录发现插件的内置来源。
/// </summary>
internal sealed class DirectoryBingPluginSource : IBingPluginSource
{
    /// <summary>
    /// 插件目录或父目录路径。
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// 是否枚举一级子目录。
    /// </summary>
    private readonly bool _folder;

    /// <summary>
    /// 初始化目录插件来源。
    /// </summary>
    /// <param name="directory">目录路径。</param>
    /// <param name="folder">是否把路径作为插件父目录。</param>
    public DirectoryBingPluginSource(string directory, bool folder)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _folder = folder;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPluginDirectories()
    {
        if (_folder)
        {
            if (!Directory.Exists(_directory))
                throw new DirectoryNotFoundException("插件父目录不存在: " + _directory);
            return Directory.EnumerateDirectories(_directory, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal);
        }

        return new[] { _directory };
    }
}

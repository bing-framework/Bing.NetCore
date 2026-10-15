using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

/// <summary>
/// 解析插件清单并加载启动模块。
/// </summary>
internal static partial class BingPluginLoader
{
    /// <summary>
    /// 插件清单文件名。
    /// </summary>
    private const string ManifestFileName = "bing-plugin.json";

    /// <summary>
    /// 插件 ID 异常数据键。
    /// </summary>
    private const string PluginIdDataKey = "Bing.PluginId";

    /// <summary>
    /// 插件清单路径异常数据键。
    /// </summary>
    private const string PluginManifestDataKey = "Bing.PluginManifest";

    /// <summary>
    /// 插件处理阶段异常数据键。
    /// </summary>
    private const string PluginPhaseDataKey = "Bing.PluginPhase";

    /// <summary>
    /// 插件程序集异常数据键。
    /// </summary>
    private const string PluginAssemblyDataKey = "Bing.PluginAssembly";

    /// <summary>
    /// 插件类型异常数据键。
    /// </summary>
    private const string PluginTypeDataKey = "Bing.PluginType";

    /// <summary>
    /// 插件依赖路径异常数据键。
    /// </summary>
    private const string PluginDependencyPathDataKey = "Bing.PluginDependencyPath";

    /// <summary>
    /// 从插件来源构建已验证的插件目录。
    /// </summary>
    /// <param name="sources">插件来源集合。</param>
    /// <param name="loadContext">可选的独立程序集加载上下文。</param>
    /// <param name="onLoaded">临时程序集解析仍可用时执行的模块预检。</param>
    /// <returns>插件描述符和启动模块集合。</returns>
    public static BingPluginLoadResult Load(IEnumerable<IBingPluginSource> sources, AssemblyLoadContext loadContext = null,
        Action<BingPluginLoadResult> onLoaded = null)
    {
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));

        var records = Discover(sources);
        if (records.Count == 0)
        {
            var empty = new BingPluginLoadResult(Array.Empty<BingPluginDescriptor>(), Array.Empty<Type>());
            onLoaded?.Invoke(empty);
            return empty;
        }

        var ordered = OrderPlugins(records);
        for (var index = 0; index < ordered.Count; index++)
            ordered[index].ExecutionOrder = index;
        var context = loadContext ?? AssemblyLoadContext.Default;
        var isolated = !ReferenceEquals(context, AssemblyLoadContext.Default);
        var candidates = BuildAssemblyCatalog(ordered, isolated);
        var resolver = new PluginAssemblyResolver(candidates, isolated);
        context.Resolving += resolver.Resolve;
        try
        {
            var preparedAssemblies = new HashSet<Assembly>();
            var startupModules = new List<Type>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var record = ordered[index];
                try
                {
                    record.EntryAssembly = LoadAssembly(record.EntryAssemblyPath, context);
                    PreloadPrivateDependencies(record.EntryAssembly, context, candidates, resolver, preparedAssemblies);
                    record.StartupModules = ResolveStartupModules(record);
                    startupModules.AddRange(record.StartupModules);
                }
                catch (Exception error)
                {
                    throw AttachPluginContext(error, record, "LoadAssembly");
                }
            }

            var descriptors = ordered.Select(record =>
                new BingPluginDescriptor(record.Id, ParseNumericVersion(record.VersionText), record.VersionText,
                    record.DirectoryPath, record.EntryAssemblyPath,
                    record.EntryAssembly, record.StartupModules,
                    record.Dependencies.Select(dependency =>
                        new BingPluginDependencyDescriptor(dependency.Id,
                            dependency.ExactVersion == null ? null :
                                ParseNumericVersion(dependency.VersionRequirement.Trim('[', ']').Split(',')[0]),
                            dependency.VersionRequirement)),
                    record.ExecutionOrder)).ToArray();
            var result = new BingPluginLoadResult(
                new ReadOnlyCollection<BingPluginDescriptor>(descriptors),
                new ReadOnlyCollection<Type>(startupModules));
            try { onLoaded?.Invoke(result); }
            catch (Exception error)
            {
                var path = error.Data["Bing.ModuleDependencyPath"] as Type[];
                var scanOwners = error.Data["Bing.ScanOwners"] as string[];
                var owner = ordered.FirstOrDefault(record => path != null && path.Any(type =>
                    type != null && ReferenceEquals(type.Assembly, record.EntryAssembly)) ||
                    scanOwners != null && scanOwners.Any(moduleName =>
                        record.StartupModules.Any(module => module.FullName == moduleName) ||
                        record.EntryAssembly.GetType(moduleName, false) != null));
                if (owner != null)
                    AttachPluginContext(error, owner, "PrepareModules",
                        assembly: error.Data["Bing.ScanAssembly"] as string);
                throw;
            }
            foreach (var candidate in candidates.Values)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(item), context) &&
                    string.Equals(item.FullName, candidate.Name.FullName, StringComparison.Ordinal));
                if (assembly == null) continue;
                try { PreloadPrivateDependencies(assembly, context, candidates, resolver, preparedAssemblies); }
                catch (Exception error) { throw AttachPluginContext(error, candidate.Record, "LoadAssembly"); }
            }
            return result;
        }
        finally
        {
            context.Resolving -= resolver.Resolve;
        }
    }
}

/// <summary>
/// 启动期插件加载结果。
/// </summary>
internal sealed class BingPluginLoadResult
{
    /// <summary>
    /// 初始化一个 <see cref="BingPluginLoadResult" /> 类型的实例。
    /// </summary>
    /// <param name="plugins">按依赖顺序排列的插件描述符。</param>
    /// <param name="startupModules">插件声明的启动模块。</param>
    public BingPluginLoadResult(IReadOnlyList<BingPluginDescriptor> plugins, IReadOnlyList<Type> startupModules)
    {
        Plugins = plugins;
        StartupModules = startupModules;
    }

    /// <summary>
    /// 获取插件描述符集合。
    /// </summary>
    public IReadOnlyList<BingPluginDescriptor> Plugins { get; }

    /// <summary>
    /// 获取插件启动模块类型集合。
    /// </summary>
    public IReadOnlyList<Type> StartupModules { get; }
}

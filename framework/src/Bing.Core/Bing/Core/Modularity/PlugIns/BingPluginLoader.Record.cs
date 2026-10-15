using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

internal static partial class BingPluginLoader
{
    /// <summary>
    /// 保存清单解析后的插件数据。
    /// </summary>
    private sealed class PluginRecord
    {
        /// <summary>
        /// 初始化一个 <see cref="PluginRecord" /> 类型的实例。
        /// </summary>
        /// <param name="manifestPath">清单文件的完整路径。</param>
        /// <param name="directoryPath">插件目录的完整路径。</param>
        /// <param name="id">插件标识。</param>
        /// <param name="version">已解析的插件版本。</param>
        /// <param name="versionText">清单中的版本文本。</param>
        /// <param name="entryAssemblyPath">入口程序集的完整路径。</param>
        /// <param name="startupModuleNames">启动模块的完整类型名。</param>
        /// <param name="dependencies">已解析的直接依赖。</param>
        public PluginRecord(string manifestPath, string directoryPath, string id, NuGetVersion version, string versionText,
            string entryAssemblyPath,
            string[] startupModuleNames, IEnumerable<PluginDependencyRecord> dependencies)
        {
            ManifestPath = manifestPath;
            DirectoryPath = directoryPath;
            Id = id;
            Version = version;
            VersionText = versionText;
            EntryAssemblyPath = entryAssemblyPath;
            StartupModuleNames = startupModuleNames;
            Dependencies = dependencies.ToList();
        }

        /// <summary>
        /// 获取清单文件路径。
        /// </summary>
        public string ManifestPath { get; }
        /// <summary>
        /// 获取插件目录路径。
        /// </summary>
        public string DirectoryPath { get; }

        /// <summary>
        /// 获取插件标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取已解析的插件版本。
        /// </summary>
        public NuGetVersion Version { get; }
        /// <summary>
        /// 获取清单中的版本文本。
        /// </summary>
        public string VersionText { get; }
        /// <summary>
        /// 获取入口程序集路径。
        /// </summary>
        public string EntryAssemblyPath { get; }
        /// <summary>
        /// 获取启动模块的完整类型名。
        /// </summary>
        public string[] StartupModuleNames { get; }
        /// <summary>
        /// 获取直接插件依赖记录。
        /// </summary>
        public List<PluginDependencyRecord> Dependencies { get; }
        /// <summary>
        /// 获取或设置插件拓扑执行序号。
        /// </summary>
        public int ExecutionOrder { get; set; }
        /// <summary>
        /// 获取或设置已加载的入口程序集。
        /// </summary>
        public Assembly EntryAssembly { get; set; }
        /// <summary>
        /// 获取或设置已解析的启动模块。
        /// </summary>
        public IReadOnlyList<Type> StartupModules { get; set; }
    }

    /// <summary>
    /// 保存插件依赖 ID 和版本。
    /// </summary>
    private sealed class PluginDependencyRecord
    {
        /// <summary>
        /// 初始化一个 <see cref="PluginDependencyRecord" /> 类型的实例。
        /// </summary>
        /// <param name="id">依赖插件标识。</param>
        /// <param name="versionRequirement">清单中的版本要求。</param>
        /// <param name="versionRange">已解析的版本区间。</param>
        public PluginDependencyRecord(string id, string versionRequirement, VersionRange versionRange)
        {
            Id = id;
            VersionRequirement = versionRequirement;
            VersionRange = versionRange;
        }

        /// <summary>
        /// 获取依赖插件标识。
        /// </summary>
        public string Id { get; }
        /// <summary>
        /// 获取清单中的版本要求。
        /// </summary>
        public string VersionRequirement { get; }
        /// <summary>
        /// 获取已解析的版本区间。
        /// </summary>
        public VersionRange VersionRange { get; }
        /// <summary>
        /// 获取精确依赖版本；区间依赖返回 null。
        /// </summary>
        public NuGetVersion ExactVersion => VersionRange.MinVersion != null &&
            VersionRange.MaxVersion != null && VersionRange.IsMinInclusive && VersionRange.IsMaxInclusive &&
            Equals(VersionRange.MinVersion, VersionRange.MaxVersion) ? VersionRange.MinVersion : null;
        /// <summary>
        /// 获取版本要求是否允许预发布版本。
        /// </summary>
        public bool AllowsPrerelease => ExactVersion?.IsPrerelease == true ||
            VersionRange.MinVersion?.IsPrerelease == true || VersionRange.MaxVersion?.IsPrerelease == true;
    }
}

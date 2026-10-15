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
    /// 保存从 JSON 清单反序列化的插件字段。
    /// </summary>
    [DataContract]
    private sealed class BingPluginManifest
    {
        /// <summary>
        /// 获取或设置插件标识。
        /// </summary>
        [DataMember(Name = "id")]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置插件版本文本。
        /// </summary>
        [DataMember(Name = "version")]
        public string Version { get; set; }

        /// <summary>
        /// 获取或设置入口程序集相对路径。
        /// </summary>
        [DataMember(Name = "entryAssembly")]
        public string EntryAssembly { get; set; }

        /// <summary>
        /// 获取或设置启动模块的完整类型名。
        /// </summary>
        [DataMember(Name = "startupModules")]
        public string[] StartupModules { get; set; }

        /// <summary>
        /// 获取或设置直接插件依赖。
        /// </summary>
        [DataMember(Name = "dependencies")]
        public BingPluginDependency[] Dependencies { get; set; }
    }

    /// <summary>
    /// 保存清单中的直接插件依赖。
    /// </summary>
    [DataContract]
    private sealed class BingPluginDependency
    {
        /// <summary>
        /// 获取或设置依赖插件的标识。
        /// </summary>
        [DataMember(Name = "id")]
        public string Id { get; set; }

        /// <summary>
        /// 获取或设置依赖版本要求。
        /// </summary>
        [DataMember(Name = "version")]
        public string Version { get; set; }
    }
}

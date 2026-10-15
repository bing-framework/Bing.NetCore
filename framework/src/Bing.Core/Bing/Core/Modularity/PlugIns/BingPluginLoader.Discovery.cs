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
    /// 发现并解析插件清单。
    /// </summary>
    /// <param name="sources">本次注册的插件来源。</param>
    /// <returns>已去重的插件解析记录。</returns>
    private static List<PluginRecord> Discover(IEnumerable<IBingPluginSource> sources)
    {
        var pluginDirectories = new List<string>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (source == null)
                throw new InvalidOperationException("插件来源不能为 null。");

            IEnumerable<string> sourceDirectories;
            try { sourceDirectories = source.GetPluginDirectories(); }
            catch (Exception error)
            {
                var sourceError = new InvalidOperationException("获取插件来源目录失败。", error);
                sourceError.Data[PluginPhaseDataKey] = "Discover";
                throw sourceError;
            }

            if (sourceDirectories == null)
            {
                var sourceError = new InvalidOperationException("插件来源不能返回 null 目录集合。");
                sourceError.Data[PluginPhaseDataKey] = "Discover";
                throw sourceError;
            }

            foreach (var directory in sourceDirectories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                    throw new InvalidOperationException("插件目录不能为 null 或空字符串。");

                var fullDirectory = Path.GetFullPath(directory);
                if (!directories.Add(fullDirectory))
                    continue;
                pluginDirectories.Add(fullDirectory);
            }
        }

        // 先完成全部来源的目录枚举和规范化，再读取清单，避免来源顺序影响预检边界。
        var records = pluginDirectories.Select(ParseManifest).ToList();

        var duplicate = records.GroupBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            var first = duplicate.First();
            var duplicateError = new InvalidOperationException("插件 ID 重复: " + duplicate.Key + "。目录: " +
                string.Join(", ", duplicate.Select(record => record.DirectoryPath)));
            duplicateError.Data[PluginIdDataKey] = first.Id;
            duplicateError.Data[PluginManifestDataKey] = first.ManifestPath;
            duplicateError.Data[PluginPhaseDataKey] = "ValidateManifest";
            throw duplicateError;
        }

        return records;
    }

    /// <summary>
    /// 读取并校验单个插件清单。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    /// <returns>插件解析记录。</returns>
    private static PluginRecord ParseManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        try
        {
            return ParseManifestCore(directory, manifestPath);
        }
        catch (Exception error)
        {
            error.Data[PluginManifestDataKey] = manifestPath;
            error.Data[PluginPhaseDataKey] = "ReadManifest";
            throw;
        }
    }

    /// <summary>
    /// 读取并校验单个插件清单的具体内容。
    /// </summary>
    /// <param name="directory">插件目录。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>插件解析记录。</returns>
    private static PluginRecord ParseManifestCore(string directory, string manifestPath)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("插件目录不存在: " + directory);

        if (!File.Exists(manifestPath))
            throw new InvalidOperationException("插件清单不存在: " + manifestPath);

        BingPluginManifest manifest;
        try
        {
            var text = File.ReadAllText(manifestPath, Encoding.UTF8);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var serializer = new DataContractJsonSerializer(typeof(BingPluginManifest));
            manifest = serializer.ReadObject(stream) as BingPluginManifest;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException("插件清单解析失败: " + manifestPath, error);
        }

        if (manifest == null)
            throw new InvalidOperationException("插件清单内容为空: " + manifestPath);

        var id = RequireText(manifest.Id, "id", manifestPath);
        try
        {
            var versionText = RequireText(manifest.Version, "version", manifestPath);
            var version = ParseVersion(versionText, "version", manifestPath);
            var entryAssembly = RequireText(manifest.EntryAssembly, "entryAssembly", manifestPath);
            if (Path.IsPathRooted(entryAssembly) || !entryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("插件入口程序集必须是目录内的相对 DLL: " + manifestPath);

            var entryPath = Path.GetFullPath(Path.Combine(directory, entryAssembly));
            EnsureInsideDirectory(directory, entryPath, manifestPath);
            if (!File.Exists(entryPath))
                throw new InvalidOperationException("插件入口程序集不存在: " + entryPath);

            if (manifest.StartupModules == null || manifest.StartupModules.Length == 0)
                throw new InvalidOperationException("插件必须至少声明一个 startupModules: " + manifestPath);
            var startupModules = manifest.StartupModules
                .Select((type, index) => RequireText(type, "startupModules[" + index + "]", manifestPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (startupModules.Length != manifest.StartupModules.Length)
                throw new InvalidOperationException("插件启动模块不能重复: " + manifestPath);

            var dependencies = new List<PluginDependencyRecord>();
            foreach (var dependency in manifest.Dependencies ?? Array.Empty<BingPluginDependency>())
            {
                if (dependency == null)
                    throw new InvalidOperationException("插件依赖项不能为 null: " + manifestPath);
                var dependencyId = RequireText(dependency.Id, "dependencies.id", manifestPath);
                var requirementText = RequireText(dependency.Version, "dependencies.version", manifestPath);
                var dependencyVersion = ParseVersionRequirement(requirementText, manifestPath);
                if (dependencies.Any(item => string.Equals(item.Id, dependencyId, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("插件依赖不能重复: " + dependencyId + "，清单: " + manifestPath);
                dependencies.Add(new PluginDependencyRecord(dependencyId, requirementText, dependencyVersion));
            }

            return new PluginRecord(manifestPath, directory, id, version, versionText, entryPath, startupModules, dependencies);
        }
        catch (Exception error)
        {
            // 已经解析出 ID 时保留它，便于定位字段、路径和入口模块错误。
            error.Data[PluginIdDataKey] = id;
            throw;
        }
    }
}

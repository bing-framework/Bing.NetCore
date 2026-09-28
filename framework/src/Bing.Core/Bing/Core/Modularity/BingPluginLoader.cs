using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using NuGet.Versioning;

namespace Bing.Core.Modularity;

/// <summary>
/// 启动期插件加载结果。
/// </summary>
internal sealed class BingPluginLoadResult
{
    /// <summary>
    /// 初始化插件加载结果。
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

/// <summary>
/// 解析插件清单并加载启动模块。
/// </summary>
internal static class BingPluginLoader
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
            var startupModules = new List<Type>();
            for (var index = 0; index < ordered.Count; index++)
            {
                var record = ordered[index];
                try
                {
                    record.EntryAssembly = LoadAssembly(record.EntryAssemblyPath, context);
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
            return result;
        }
        finally
        {
            context.Resolving -= resolver.Resolve;
        }
    }

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

    /// <summary>
    /// 按插件依赖关系排序并校验版本要求。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <returns>依赖优先的插件序列。</returns>
    private static List<PluginRecord> OrderPlugins(IReadOnlyList<PluginRecord> records)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            foreach (var dependency in record.Dependencies)
            {
                if (!byId.TryGetValue(dependency.Id, out var depended))
                {
                    var missingPath = FindDependencyPath(records, record.Id)
                        .Concat(new[] { dependency.Id });
                    throw AttachPluginContext(new InvalidOperationException(
                        "插件依赖不存在: " + string.Join(" -> ", missingPath)), record, "ValidateDependencies",
                        dependencyPath: missingPath);
                }
                if (!dependency.VersionRange.Satisfies(depended.Version) ||
                    (depended.Version.IsPrerelease && !dependency.AllowsPrerelease))
                {
                    var versionPath = FindDependencyPath(records, record.Id)
                        .Concat(new[] { dependency.Id });
                    throw AttachPluginContext(new InvalidOperationException(
                        "插件依赖版本不匹配: " + string.Join(" -> ", versionPath) + "，要求 " + dependency.Id + " " + dependency.VersionRequirement +
                        "，实际为 " + depended.VersionText), record, "ValidateDependencies",
                        dependencyPath: versionPath);
                }
            }
        }

        var ordered = new List<PluginRecord>(records.Count);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (ordered.Count < records.Count)
        {
            var ready = records.Where(record => !completed.Contains(record.Id) &&
                    record.Dependencies.All(dependency => completed.Contains(dependency.Id)))
                .OrderBy(record => record.Id, StringComparer.Ordinal)
                .ToArray();
            if (ready.Length == 0)
            {
                var cycle = FindPluginCycle(records, completed);
                var cycleError = new InvalidOperationException("插件依赖存在环: " + string.Join(" -> ", cycle));
                var first = records.FirstOrDefault(record => string.Equals(record.Id, cycle[0], StringComparison.OrdinalIgnoreCase));
                if (first != null)
                    throw AttachPluginContext(cycleError, first, "ValidateDependencies", dependencyPath: cycle);
                throw cycleError;
            }

            foreach (var record in ready)
            {
                ordered.Add(record);
                completed.Add(record.Id);
            }
        }
        return ordered;
    }

    /// <summary>
    /// 查找插件依赖环并返回完整路径。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <param name="completed">已完成排序的插件标识。</param>
    /// <returns>插件依赖环路径。</returns>
    private static IReadOnlyList<string> FindPluginCycle(IEnumerable<PluginRecord> records, ISet<string> completed)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(completed, StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        foreach (var record in records.OrderBy(record => record.Id, StringComparer.Ordinal))
        {
            if (visited.Contains(record.Id))
                continue;
            var cycle = Visit(record.Id);
            if (cycle != null)
                return cycle;
        }
        return records.Select(record => record.Id).ToArray();

        IReadOnlyList<string> Visit(string id)
        {
            if (visited.Contains(id))
                return null;
            if (visiting.Contains(id))
            {
                return path.Concat(new[] { id }).ToArray();
            }

            visiting.Add(id);
            path.Add(id);
            foreach (var dependency in byId[id].Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                var cycle = Visit(dependency.Id);
                if (cycle != null)
                    return cycle;
            }
            path.RemoveAt(path.Count - 1);
            visiting.Remove(id);
            visited.Add(id);
            return null;
        }
    }

    /// <summary>
    /// 查找从插件图入口到指定插件的依赖路径。
    /// </summary>
    /// <param name="records">已解析的插件记录。</param>
    /// <param name="targetId">目标插件 ID。</param>
    /// <returns>包含目标插件的依赖路径；无法从入口到达时返回目标插件自身。</returns>
    private static IReadOnlyList<string> FindDependencyPath(IReadOnlyList<PluginRecord> records, string targetId)
    {
        var byId = records.ToDictionary(record => record.Id, StringComparer.OrdinalIgnoreCase);
        var dependedOn = new HashSet<string>(records.SelectMany(record => record.Dependencies)
            .Where(dependency => byId.ContainsKey(dependency.Id))
            .Select(dependency => dependency.Id), StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();
        var roots = records.Where(record => !dependedOn.Contains(record.Id))
            .OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
        var starts = roots.Length > 0 ? roots : records.OrderBy(record => record.Id, StringComparer.Ordinal).ToArray();
        foreach (var root in starts)
        {
            if (Visit(root.Id))
                return path.ToArray();
        }
        return new[] { targetId };

        bool Visit(string id)
        {
            if (path.Any(value => string.Equals(value, id, StringComparison.OrdinalIgnoreCase)))
                return false;
            path.Add(id);
            if (string.Equals(id, targetId, StringComparison.OrdinalIgnoreCase))
                return true;
            foreach (var dependency in byId[id].Dependencies.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (byId.ContainsKey(dependency.Id) && Visit(dependency.Id))
                    return true;
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    /// <summary>
    /// 建立插件目录的程序集候选表并检查同名冲突。
    /// </summary>
    /// <param name="records">依赖优先的插件序列。</param>
    /// <param name="isolated">是否使用独立加载上下文。</param>
    /// <returns>按程序集短名称索引的候选表。</returns>
    private static IReadOnlyDictionary<string, AssemblyCandidate> BuildAssemblyCatalog(IReadOnlyList<PluginRecord> records,
        bool isolated = false)
    {
        var candidates = new Dictionary<string, List<AssemblyCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(record.DirectoryPath, "*.dll", SearchOption.AllDirectories); }
            catch (Exception error) { throw AttachPluginContext(error, record, "ScanAssemblies", assembly: record.DirectoryPath); }

            foreach (var file in files.OrderBy(path => path, StringComparer.Ordinal))
            {
                AssemblyName name;
                try { name = AssemblyName.GetAssemblyName(file); }
                catch (Exception error)
                {
                    throw AttachPluginContext(new InvalidOperationException("插件目录中存在无法读取的程序集: " + file, error), record,
                        "ScanAssemblies", assembly: file);
                }

                if (string.IsNullOrWhiteSpace(name.Name))
                    continue;
                if (!candidates.TryGetValue(name.Name, out var values))
                    candidates[name.Name] = values = new List<AssemblyCandidate>();
                values.Add(new AssemblyCandidate(name, file, record));
            }
        }

        var result = new Dictionary<string, AssemblyCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in candidates)
        {
            var identities = pair.Value.GroupBy(candidate => candidate.Name.FullName, StringComparer.Ordinal)
                .ToArray();
            if (identities.Length > 1)
            {
                var details = string.Join("; ", pair.Value.Select(candidate => candidate.Name.FullName + " @ " + candidate.Path));
                var conflict = new InvalidOperationException("插件程序集同名但版本不同: " + pair.Key + ". " + details);
                var first = pair.Value[0].Record;
                conflict.Data[PluginIdDataKey] = first.Id;
                conflict.Data[PluginManifestDataKey] = first.ManifestPath;
                conflict.Data[PluginPhaseDataKey] = "ScanAssemblies";
                conflict.Data[PluginAssemblyDataKey] = details;
                throw conflict;
            }

            var selected = pair.Value.OrderBy(candidate => candidate.Record.ExecutionOrder)
                .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
                .First();
            var loaded = isolated ? null : AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default))
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, pair.Key, StringComparison.OrdinalIgnoreCase));
            if (loaded != null && !string.Equals(loaded.GetName().FullName, selected.Name.FullName, StringComparison.Ordinal))
            {
                var conflict = new InvalidOperationException("插件程序集与宿主已加载程序集版本冲突: " + pair.Key + ". 插件为 " +
                    selected.Name.FullName + "，宿主为 " + loaded.GetName().FullName);
                conflict.Data[PluginIdDataKey] = selected.Record.Id;
                conflict.Data[PluginManifestDataKey] = selected.Record.ManifestPath;
                conflict.Data[PluginPhaseDataKey] = "ScanAssemblies";
                conflict.Data[PluginAssemblyDataKey] = selected.Name.FullName;
                throw conflict;
            }
            result[pair.Key] = selected;
        }
        return result;
    }

    /// <summary>
    /// 加载入口程序集，并优先复用当前加载上下文中的同一路径程序集。
    /// </summary>
    /// <param name="path">入口程序集路径。</param>
    /// <param name="context">目标加载上下文。</param>
    /// <returns>已加载的入口程序集。</returns>
    private static Assembly LoadAssembly(string path, AssemblyLoadContext context)
    {
        if (!ReferenceEquals(context, AssemblyLoadContext.Default))
        {
            var local = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), context) &&
                string.Equals(GetAssemblyLocation(assembly), path, StringComparison.OrdinalIgnoreCase));
            return local ?? context.LoadFromAssemblyPath(path);
        }
        var existing = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default))
            .FirstOrDefault(assembly => string.Equals(GetAssemblyLocation(assembly), path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;
        var identity = AssemblyName.GetAssemblyName(path).FullName;
        existing = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default))
            .FirstOrDefault(assembly => string.Equals(assembly.GetName().FullName, identity, StringComparison.Ordinal));
        if (existing != null)
            return existing;
        return context.LoadFromAssemblyPath(path);
    }

    /// <summary>
    /// 解析入口程序集声明的启动模块类型。
    /// </summary>
    /// <param name="record">所属插件记录。</param>
    /// <returns>已验证的启动模块类型。</returns>
    private static IReadOnlyList<Type> ResolveStartupModules(PluginRecord record)
    {
        var modules = new List<Type>();
        foreach (var name in record.StartupModuleNames)
        {
            var type = record.EntryAssembly.GetType(name, false, false);
            if (type == null || type.Assembly != record.EntryAssembly || !type.IsPublic || !BingModule.IsBingModule(type))
            {
                var error = new InvalidOperationException("插件启动模块不是有效的公开 BingModule: " + name);
                error.Data[PluginTypeDataKey] = name;
                throw error;
            }
            modules.Add(type);
        }
        return new ReadOnlyCollection<Type>(modules);
    }

    /// <summary>
    /// 将文件路径限制在插件目录中。
    /// </summary>
    /// <param name="directory">插件根目录。</param>
    /// <param name="path">待检查的文件路径。</param>
    /// <param name="manifestPath">用于错误诊断的清单路径。</param>
    private static void EnsureInsideDirectory(string directory, string path, string manifestPath)
    {
        var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("插件入口程序集不能离开插件目录: " + manifestPath);
    }

    /// <summary>
    /// 获取必填文本字段。
    /// </summary>
    /// <param name="value">字段原始文本。</param>
    /// <param name="field">字段名称。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>去除首尾空白的字段值。</returns>
    private static string RequireText(string value, string field, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("插件清单字段不能为空: " + field + "，清单: " + manifestPath);
        return value.Trim();
    }

    /// <summary>
    /// 解析三段或四段插件版本及可选的预发布和构建标识。
    /// </summary>
    /// <param name="value">版本文本。</param>
    /// <param name="field">版本字段名称。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>已解析的插件版本。</returns>
    private static NuGetVersion ParseVersion(string value, string field, string manifestPath)
    {
        var text = RequireText(value, field, manifestPath);
        var numeric = text.Split('-', '+')[0].Split('.');
        if (numeric.Length < 3 || numeric.Length > 4 ||
            numeric.Any(part => !int.TryParse(part, out var number) || number < 0) ||
            !NuGetVersion.TryParseStrict(text, out var version))
            throw new InvalidOperationException("插件版本必须是三段或四段非负数字，可带合法预发布标识: " + text + "，清单: " + manifestPath);
        return version;
    }

    /// <summary>
    /// 解析精确版本或 NuGet 区间约束。
    /// </summary>
    /// <param name="text">依赖版本要求。</param>
    /// <param name="manifestPath">清单文件路径。</param>
    /// <returns>已解析的版本区间。</returns>
    private static VersionRange ParseVersionRequirement(string text, string manifestPath)
    {
        if (text[0] != '[' && text[0] != '(')
        {
            var exact = ParseVersion(text, "dependencies.version", manifestPath);
            return new VersionRange(exact, true, exact, true);
        }
        if (text.Length < 3 || (text[text.Length - 1] != ']' && text[text.Length - 1] != ')'))
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        var bounds = text.Substring(1, text.Length - 2).Split(',');
        if (bounds.Length < 1 || bounds.Length > 2)
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        foreach (var bound in bounds.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try { ParseVersion(bound.Trim(), "dependencies.version", manifestPath); }
            catch (InvalidOperationException error)
            {
                throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath, error);
            }
        }
        if (!VersionRange.TryParse(text, out var range) ||
            (range.MinVersion == null && range.MaxVersion == null) ||
            (range.MinVersion != null && range.MaxVersion != null &&
                (range.MinVersion.CompareTo(range.MaxVersion) > 0 ||
                 (range.MinVersion.CompareTo(range.MaxVersion) == 0 &&
                  (!range.IsMinInclusive || !range.IsMaxInclusive)))))
            throw new InvalidOperationException("插件依赖版本区间无效: " + text + "，清单: " + manifestPath);
        return range;
    }

    /// <summary>
    /// 从完整版本中提取兼容旧契约的三段或四段数值版本。
    /// </summary>
    /// <param name="text">完整版本文本。</param>
    /// <returns>三段或四段数值版本。</returns>
    private static Version ParseNumericVersion(string text) => Version.Parse(text.Split('-', '+')[0]);

    /// <summary>
    /// 附加插件上下文并保留原始异常类型。
    /// </summary>
    /// <param name="error">原始异常。</param>
    /// <param name="record">发生错误的插件记录。</param>
    /// <param name="phase">失败阶段。</param>
    /// <param name="assembly">相关程序集路径。</param>
    /// <param name="dependencyPath">相关插件依赖链。</param>
    /// <returns>附加插件诊断信息的原始异常。</returns>
    private static Exception AttachPluginContext(Exception error, PluginRecord record, string phase,
        string assembly = null, IEnumerable<string> dependencyPath = null)
    {
        error.Data[PluginIdDataKey] = record.Id;
        error.Data[PluginManifestDataKey] = record.ManifestPath;
        error.Data[PluginPhaseDataKey] = phase;
        if (!error.Data.Contains(PluginAssemblyDataKey))
            error.Data[PluginAssemblyDataKey] = assembly ?? record.EntryAssemblyPath;
        if (dependencyPath != null)
            error.Data[PluginDependencyPathDataKey] = dependencyPath.ToArray();
        return error;
    }

    /// <summary>
    /// 获取程序集文件位置。
    /// </summary>
    /// <param name="assembly">目标程序集。</param>
    /// <returns>程序集文件位置；位置不可用时返回空字符串。</returns>
    private static string GetAssemblyLocation(Assembly assembly)
    {
        try { return assembly.Location; }
        catch { return string.Empty; }
    }

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

    /// <summary>
    /// 保存清单解析后的插件数据。
    /// </summary>
    private sealed class PluginRecord
    {
        /// <summary>
        /// 初始化插件清单的解析记录。
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
        /// 初始化插件依赖的版本约束记录。
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

    /// <summary>
    /// 保存程序集候选文件及其所属插件。
    /// </summary>
    private sealed class AssemblyCandidate
    {
        /// <summary>
        /// 初始化插件程序集候选记录。
        /// </summary>
        /// <param name="name">程序集标识。</param>
        /// <param name="path">程序集文件路径。</param>
        /// <param name="record">候选所属的插件。</param>
        public AssemblyCandidate(AssemblyName name, string path, PluginRecord record)
        {
            Name = name;
            Path = path;
            Record = record;
        }

        /// <summary>
        /// 获取程序集标识。
        /// </summary>
        public AssemblyName Name { get; }
        /// <summary>
        /// 获取程序集文件路径。
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// 获取所属插件记录。
        /// </summary>
        public PluginRecord Record { get; }
    }

    /// <summary>
    /// 处理插件目录中的依赖程序集解析。
    /// </summary>
    private sealed class PluginAssemblyResolver
    {
        /// <summary>
        /// 当前注册批次的程序集候选表。
        /// </summary>
        private readonly IReadOnlyDictionary<string, AssemblyCandidate> _candidates;

        /// <summary>
        /// 是否只在当前可回收上下文中查找已加载程序集。
        /// </summary>
        private readonly bool _isolated;

        /// <summary>
        /// 初始化默认加载上下文的插件程序集解析器。
        /// </summary>
        /// <param name="candidates">本次注册的程序集候选表。</param>
        public PluginAssemblyResolver(IReadOnlyDictionary<string, AssemblyCandidate> candidates) : this(candidates, false) { }

        /// <summary>
        /// 初始化插件程序集解析器。
        /// </summary>
        /// <param name="candidates">本次注册的程序集候选表。</param>
        /// <param name="isolated">是否在独立上下文中解析。</param>
        public PluginAssemblyResolver(IReadOnlyDictionary<string, AssemblyCandidate> candidates, bool isolated)
        {
            _candidates = candidates;
            _isolated = isolated;
        }

        /// <summary>
        /// 解析当前插件批次引用的程序集。
        /// </summary>
        /// <param name="context">发起解析的加载上下文。</param>
        /// <param name="name">请求的程序集标识。</param>
        /// <returns>匹配的程序集；候选不存在时返回 null。</returns>
        public Assembly Resolve(AssemblyLoadContext context, AssemblyName name)
        {
            if (name == null || string.IsNullOrWhiteSpace(name.Name))
                return null;
            if (_isolated)
            {
                var contracts = new[]
                {
                    typeof(BingModule).Assembly,
                    typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly
                };
                foreach (var contract in contracts)
                {
                    if (!string.Equals(contract.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.Equals(contract.GetName().FullName, name.FullName, StringComparison.Ordinal))
                        throw new FileLoadException("插件引用的宿主契约程序集版本不匹配: " + name.FullName);
                    return contract;
                }
            }
            var loadedAssemblies = _isolated
                ? AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), context))
                : AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                    ReferenceEquals(AssemblyLoadContext.GetLoadContext(assembly), AssemblyLoadContext.Default));
            var loaded = loadedAssemblies.FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
            if (loaded != null)
            {
                var loadedName = loaded.GetName();
                if (!string.Equals(loadedName.FullName, name.FullName, StringComparison.Ordinal))
                {
                    var error = new FileLoadException("插件依赖程序集与已加载程序集版本不匹配: " + name.FullName +
                        "，已加载为 " + loadedName.FullName);
                    error.Data[PluginAssemblyDataKey] = name.FullName;
                    throw error;
                }
                return loaded;
            }
            if (!_candidates.TryGetValue(name.Name, out var candidate))
                return null;
            if (!string.Equals(candidate.Name.FullName, name.FullName, StringComparison.Ordinal))
            {
                var error = new FileLoadException("插件依赖程序集版本不匹配: " + name.FullName + "，候选为 " + candidate.Name.FullName);
                error.Data[PluginAssemblyDataKey] = candidate.Name.FullName;
                throw error;
            }
            return context.LoadFromAssemblyPath(candidate.Path);
        }
    }
}

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
    /// 预加载已部署的插件私有依赖闭包。
    /// </summary>
    /// <remarks>仅加载静态引用可达的候选程序集，不扩大服务扫描范围；缺失的可选引用仍由实际使用路径报告。</remarks>
    /// <param name="assembly">依赖起点。</param>
    /// <param name="context">目标加载上下文。</param>
    /// <param name="candidates">本次注册的程序集候选表。</param>
    /// <param name="resolver">本次注册的程序集解析器。</param>
    /// <param name="visited">已处理的程序集。</param>
    private static void PreloadPrivateDependencies(Assembly assembly, AssemblyLoadContext context,
        IReadOnlyDictionary<string, AssemblyCandidate> candidates, PluginAssemblyResolver resolver,
        HashSet<Assembly> visited)
    {
        var pending = new Stack<Assembly>();
        pending.Push(assembly);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current)) continue;
            foreach (var reference in current.GetReferencedAssemblies())
            {
                if (!candidates.ContainsKey(reference.Name)) continue;
                var dependency = resolver.Resolve(context, reference);
                // 宿主共享契约的依赖由宿主负责，不能复制到插件上下文。
                if (dependency != null && ReferenceEquals(AssemblyLoadContext.GetLoadContext(dependency), context))
                    pending.Push(dependency);
            }
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
            if (loaded != null && !PluginAssemblyResolver.HasSameAssemblyIdentity(loaded.GetName(), selected.Name))
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
    /// 保存程序集候选文件及其所属插件。
    /// </summary>
    private sealed class AssemblyCandidate
    {
        /// <summary>
        /// 初始化一个 <see cref="AssemblyCandidate" /> 类型的实例。
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
}

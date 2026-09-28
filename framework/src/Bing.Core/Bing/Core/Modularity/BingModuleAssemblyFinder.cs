using System.Reflection;
using System.Runtime.Loader;
using Bing.Reflection;

namespace Bing.Core.Modularity;

/// <summary>
/// 保存单次应用注册的程序集和类型快照。
/// </summary>
internal sealed class BingModuleAssemblyFinder : IAllAssemblyFinder
{
    /// <summary>
    /// 按扫描顺序排列的程序集。
    /// </summary>
    private readonly Assembly[] _assemblies;

    /// <summary>
    /// 按程序集顺序排列的可用类型。
    /// </summary>
    private readonly Type[] _types;

    /// <summary>
    /// 初始化本次注册的扫描快照。
    /// </summary>
    /// <param name="modules">已选中的模块。</param>
    /// <param name="additionalAssemblies">应用级附加程序集。</param>
    /// <param name="loadContext">当前插件代的可选加载上下文。</param>
    internal BingModuleAssemblyFinder(IReadOnlyList<BingModuleDescriptor> modules,
        IEnumerable<Assembly> additionalAssemblies, AssemblyLoadContext loadContext)
    {
        var assemblies = new List<Assembly>();
        var declared = new HashSet<Assembly>();
        var selected = new HashSet<Assembly>();
        var owners = new Dictionary<Assembly, List<string>>();
        foreach (var module in modules)
        {
            var moduleAssemblies = new List<Assembly> { module.Assembly };
            foreach (var attribute in module.ModuleType.GetCustomAttributes<BingModuleAssemblyAttribute>(true))
            {
                var assembly = attribute.MarkerType.Assembly;
                ValidateContext(assembly, loadContext, module.ModuleType.FullName);
                moduleAssemblies.Add(assembly);
                declared.Add(assembly);
                if (!owners.TryGetValue(assembly, out var moduleOwners))
                    owners[assembly] = moduleOwners = new List<string>();
                moduleOwners.Add(module.ModuleType.FullName);
            }
            module.SetAssemblies(moduleAssemblies.Distinct());
            foreach (var assembly in module.Assemblies)
                if (selected.Add(assembly)) assemblies.Add(assembly);
        }
        foreach (var assembly in additionalAssemblies)
        {
            if (assembly == null) throw new ArgumentException("附加程序集不能为 null。", nameof(additionalAssemblies));
            ValidateContext(assembly, loadContext, "ServiceScanning.AdditionalAssemblies");
            declared.Add(assembly);
            if (!owners.TryGetValue(assembly, out var applicationOwners))
                owners[assembly] = applicationOwners = new List<string>();
            applicationOwners.Add("ServiceScanning.AdditionalAssemblies");
            if (selected.Add(assembly)) assemblies.Add(assembly);
        }
        _assemblies = assemblies.ToArray();
        var types = new List<Type>();
        foreach (var assembly in _assemblies)
        {
            try
            {
                // 显式声明的程序集必须完整可读，不能忽略部分加载失败。
                var discovered = declared.Contains(assembly) ? assembly.GetTypes() :
                    AssemblyHelper.GetAllTypes(assembly).Where(type => type != null).ToArray();
                types.AddRange(discovered);
            }
            catch (ReflectionTypeLoadException error) when (declared.Contains(assembly))
            {
                var declaredBy = owners[assembly].Distinct(StringComparer.Ordinal).ToArray();
                var loaderErrors = error.LoaderExceptions.Where(exception => exception != null)
                    .Select(exception => exception.Message).ToArray();
                var failure = new InvalidOperationException("附加程序集无法完整读取类型: " + assembly.FullName +
                    "，声明者: " + string.Join(", ", declaredBy) + "。" + string.Join("; ", loaderErrors), error);
                failure.Data["Bing.ScanPhase"] = "DiscoverTypes";
                failure.Data["Bing.ScanAssembly"] = assembly.FullName;
                failure.Data["Bing.ScanOwners"] = declaredBy;
                failure.Data["Bing.ScanLoaderErrors"] = loaderErrors;
                throw failure;
            }
        }
        _types = types.ToArray();
    }

    /// <summary>
    /// 获取本次注册的类型快照。
    /// </summary>
    /// <returns>模块图包含的类型快照。</returns>
    internal IReadOnlyList<Type> FindTypes() => _types;

    /// <inheritdoc />
    public Assembly[] FindAll(bool fromCache = false) => _assemblies.ToArray();

    /// <inheritdoc />
    public Assembly[] Find(Func<Assembly, bool> predicate, bool fromCache = false) =>
        _assemblies.Where(predicate).ToArray();

    /// <summary>
    /// 拒绝来自其他可回收插件代的程序集。
    /// </summary>
    private static void ValidateContext(Assembly assembly, AssemblyLoadContext expected, string owner)
    {
        var actual = AssemblyLoadContext.GetLoadContext(assembly);
        if (!ReferenceEquals(actual, AssemblyLoadContext.Default) && !ReferenceEquals(actual, expected))
            throw new InvalidOperationException("附加程序集属于其他插件代: " + assembly.FullName + "，声明者: " + owner + "。");
    }
}

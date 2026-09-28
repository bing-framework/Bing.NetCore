using Bing.Extensions;
using Bing.Finders;
using Bing.Reflection;

namespace Bing.DependencyInjection;

/// <summary>
/// 依赖注入类型查找器。
/// </summary>
public class DependencyTypeFinder : FinderBase<Type>, IDependencyTypeFinder
{
    /// <summary>
    /// 获取所有程序集查找器。
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 当前应用的约定服务类型筛选器。
    /// </summary>
    private Func<Type, bool> _conventionalTypeFilter;

    /// <summary>
    /// 注册完成后固定的约定服务类型。
    /// </summary>
    private Type[] _completedTypes;

    /// <summary>
    /// 初始化依赖注入类型查找器。
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器。</param>
    public DependencyTypeFinder(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

    /// <summary>
    /// 初始化带应用级类型筛选器的查找器。
    /// </summary>
    internal DependencyTypeFinder(IAllAssemblyFinder allAssemblyFinder, Func<Type, bool> conventionalTypeFilter)
    {
        _allAssemblyFinder = allAssemblyFinder;
        _conventionalTypeFilter = conventionalTypeFilter;
    }

    /// <summary>
    /// 固定本轮结果并释放只在注册期间使用的筛选器。
    /// </summary>
    internal void CompleteRegistration(bool successful)
    {
        if (_conventionalTypeFilter == null) return;
        _completedTypes = successful && Found ? ItemsCache.ToArray() : Array.Empty<Type>();
        _conventionalTypeFilter = null;
    }

    /// <inheritdoc />
    /// <remarks>仅返回符合当前应用约定服务筛选条件的类型。</remarks>
    protected override Type[] FindAllItems()
    {
        if (_completedTypes != null) return _completedTypes;
        var baseTypes = new[] {typeof(ISingletonDependency), typeof(IScopedDependency), typeof(ITransientDependency)};
        var source = _allAssemblyFinder is Bing.Core.Modularity.BingModuleAssemblyFinder frozen
            ? frozen.FindTypes()
            : _allAssemblyFinder.FindAll(true).SelectMany(assembly => AssemblyHelper.GetAllTypes(assembly))
                .OfType<Type>();
        var types = source.Where(type => type.IsClass && !type.IsAbstract && !type.IsInterface
                && !type.HasAttribute<IgnoreDependencyAttribute>()
                && (baseTypes.Any(b => b.IsAssignableFrom(type)) || type.HasAttribute<DependencyAttribute>()))
            .Where(type => Include(type)).ToArray();
        return types;
    }

    /// <summary>
    /// 应用约定服务筛选器并附上出错类型。
    /// </summary>
    /// <param name="type">待筛选的服务类型。</param>
    /// <returns>类型通过筛选时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    private bool Include(Type type)
    {
        if (_conventionalTypeFilter == null) return true;
        try { return _conventionalTypeFilter(type); }
        catch (Exception error)
        {
            var failure = new InvalidOperationException("约定服务筛选器处理类型失败: " + type.FullName, error);
            failure.Data["Bing.ServiceScanPhase"] = "RegisterConventionServices";
            failure.Data["Bing.ServiceType"] = type.FullName;
            throw failure;
        }
    }
}

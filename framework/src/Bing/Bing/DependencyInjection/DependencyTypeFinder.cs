using Bing.Extensions;
using Bing.Finders;
using Bing.Reflection;

namespace Bing.DependencyInjection;

/// <summary>
/// 依赖注入类型查找器
/// </summary>
public class DependencyTypeFinder : FinderBase<Type>, IDependencyTypeFinder
{
    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 初始化一个<see cref="DependencyTypeFinder"/>类型的实例
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public DependencyTypeFinder(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

    /// <summary>
    /// 重写已实现所有项的查找
    /// </summary>
    protected override Type[] FindAllItems()
    {
        var baseTypes = new[] {typeof(ISingletonDependency), typeof(IScopedDependency), typeof(ITransientDependency)};
        var types = _allAssemblyFinder.FindAll(true)
            .SelectMany(assembly => assembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && !type.IsInterface
                               && !type.HasAttribute<IgnoreDependencyAttribute>()
                               && (baseTypes.Any(b => b.IsAssignableFrom(type)) || type.HasAttribute<DependencyAttribute>())))
            .ToArray();
        return types;
    }
}

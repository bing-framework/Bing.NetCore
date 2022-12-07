using Bing.Extensions;
using Bing.Finders;

namespace Bing.Reflection;

/// <summary>
/// 特性类型查找器基类
/// </summary>
/// <typeparam name="TAttributeType">特性类型</typeparam>
public class AttributeTypeFinderBase<TAttributeType> : FinderBase<Type>, ITypeFinder 
    where TAttributeType : Attribute
{
    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 初始化一个<see cref="AttributeTypeFinderBase{TAttributeType}"/>类型的实例
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public AttributeTypeFinderBase(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

    /// <summary>
    /// 执行所有项目的查找工作
    /// </summary>
    protected override Type[] FindAllItems()
    {
        var assemblies = _allAssemblyFinder.FindAll(true);
        return assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsAbstract && type.HasAttribute<TAttributeType>())
            .Distinct()
            .ToArray();
    }
}

using System;
using System.Linq;
using Bing.Finders;
using Bing.Reflection;

namespace Bing.ObjectMapping;

/// <summary>
/// 对象映射配置类型查找器
/// </summary>
public class MapperProfileTypeFinder:FinderBase<Type>, IMapperProfileTypeFinder
{
    /// <summary>
    /// 所有程序集查找器
    /// </summary>
    private readonly IAllAssemblyFinder _allAssemblyFinder;

    /// <summary>
    /// 初始化一个<see cref="MapperProfileTypeFinder"/>类型的实例
    /// </summary>
    /// <param name="allAssemblyFinder">所有程序集查找器</param>
    public MapperProfileTypeFinder(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

    /// <summary>
    /// 执行所有项目的查找工作
    /// </summary>
    protected override Type[] FindAllItems()
    {
        var types = _allAssemblyFinder.FindAll(true)
            .SelectMany(assembly => assembly.GetTypes().Where(type =>
                type.IsClass && 
                !type.IsAbstract && 
                !type.IsInterface &&
                typeof(IObjectMapperProfile).IsAssignableFrom(type)))
            .ToArray();
        return types;
    }
}
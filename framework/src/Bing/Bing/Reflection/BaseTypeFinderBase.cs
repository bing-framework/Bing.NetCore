using System;
using System.Linq;
using Bing.Extensions;
using Bing.Finders;

namespace Bing.Reflection
{
    /// <summary>
    /// 指定基类的实现类型查找器基类
    /// </summary>
    /// <typeparam name="TBaseType">要查找类型的基类</typeparam>
    public abstract class BaseTypeFinderBase<TBaseType> : FinderBase<Type>, ITypeFinder
    {
        /// <summary>
        /// 所有程序集查找器
        /// </summary>
        private readonly IAllAssemblyFinder _allAssemblyFinder;

        /// <summary>
        /// 初始化一个<see cref="BaseTypeFinderBase{TBaseType}"/>类型的实例
        /// </summary>
        /// <param name="allAssemblyFinder">所有程序集查找器</param>
        protected BaseTypeFinderBase(IAllAssemblyFinder allAssemblyFinder) => _allAssemblyFinder = allAssemblyFinder;

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected override Type[] FindAllItems()
        {
            var assemblies = _allAssemblyFinder.FindAll(true);
            return assemblies.SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsDeriveClassFrom<TBaseType>())
                .Distinct()
                .ToArray();
        }
    }
}

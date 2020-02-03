using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Extensions;
using Bing.Finders;
using Bing.Helpers;
using Bing.Reflections;
using Bing.Extensions;
using Bing.Utils.Helpers;

namespace Bing.Dependency
{
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
        public DependencyTypeFinder(IAllAssemblyFinder allAssemblyFinder)
        {
            _allAssemblyFinder = allAssemblyFinder;
        }

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected override Type[] FindAllItems()
        {
            var baseTypes = new[]
                {typeof(ISingletonDependency), typeof(IScopedDependency), typeof(ITransientDependency)};
            var types = _allAssemblyFinder.FindAll(true).SelectMany(assembly => assembly.GetTypes().Where(type =>
                type.IsClass && !type.IsAbstract && !type.IsInterface &&
                !type.HasAttribute<IgnoreDependencyAttribute>() &&
                (baseTypes.Any(b => b.IsAssignableFrom(type)) || type.HasAttribute<DependencyAttribute>()))).ToArray();
            return types;
        }

        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <typeparam name="T">查找类型</typeparam>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        public virtual List<Type> Find<T>(List<Assembly> assemblies = null) => Find(typeof(T), assemblies);

        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <param name="findType">查找类型</param>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        public virtual List<Type> Find(Type findType, List<Assembly> assemblies = null)
        {
            assemblies = assemblies ?? _allAssemblyFinder.FindAll(true).ToList();
            return Reflection.FindTypes(findType, assemblies.ToArray());
        }
    }
}

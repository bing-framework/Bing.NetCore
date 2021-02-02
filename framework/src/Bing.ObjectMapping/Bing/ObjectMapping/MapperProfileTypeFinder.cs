using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Finders;
using Bing.Reflection;

namespace Bing.ObjectMapping
{
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
            assemblies ??= _allAssemblyFinder.FindAll(true).ToList();
            return Reflections.FindTypes(findType, assemblies.ToArray());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bing.Finders;
using Bing.Helpers;
using Bing.Reflection;

namespace Bing.Reflection
{
    /// <summary>
    /// 类型查找器
    /// </summary>
    public class TypeFinder : FinderBase<Type>, ITypeFinder
    {
        /// <summary>
        /// 所有程序集查找器
        /// </summary>
        private readonly IAllAssemblyFinder _allAssemblyFinder;

        /// <summary>
        /// 初始化一个<see cref="TypeFinder"/>类型的实例
        /// </summary>
        /// <param name="allAssemblyFinder">所有程序集查找器</param>
        public TypeFinder(IAllAssemblyFinder allAssemblyFinder)
        {
            _allAssemblyFinder = allAssemblyFinder;
        }

        /// <summary>
        /// 重写已实现所有项的查找
        /// </summary>
        protected override Type[] FindAllItems()
        {
            var allTypes = new List<Type>();
            foreach (var assembly in _allAssemblyFinder.FindAll(true))
            {
                var typesInThisAssembly = AssemblyHelper.GetAllTypes(assembly);
                if (!typesInThisAssembly.Any())
                    continue;
                allTypes.AddRange(typesInThisAssembly.Where(type => type != null));
            }
            return allTypes.ToArray();
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
            return Helpers.Reflections.FindTypes(findType, assemblies.ToArray());
        }
    }
}

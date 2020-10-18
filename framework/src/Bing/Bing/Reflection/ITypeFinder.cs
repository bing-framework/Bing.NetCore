using System;
using System.Collections.Generic;
using System.Reflection;
using Bing.DependencyInjection;
using Bing.Finders;

namespace Bing.Reflection
{
    /// <summary>
    /// 定义类型查找器
    /// </summary>
    [IgnoreDependency]
    public interface ITypeFinder : IFinder<Type>
    {
        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <typeparam name="T">查找类型</typeparam>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        [Obsolete]
        List<Type> Find<T>(List<Assembly> assemblies = null);

        /// <summary>
        /// 查找类型列表
        /// </summary>
        /// <param name="findType">查找类型</param>
        /// <param name="assemblies">在指定的程序集列表中查找</param>
        [Obsolete]
        List<Type> Find(Type findType, List<Assembly> assemblies = null);
    }
}

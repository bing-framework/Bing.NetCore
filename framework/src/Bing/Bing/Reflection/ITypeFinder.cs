using System;
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
    }
}

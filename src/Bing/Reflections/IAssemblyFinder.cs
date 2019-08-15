using System.Reflection;
using Bing.Dependency;
using Bing.Finders;

namespace Bing.Reflections
{
    /// <summary>
    /// 定义程序集查找器
    /// </summary>
    [IgnoreDependency]
    public interface IAssemblyFinder : IFinder<Assembly>
    {
    }
}

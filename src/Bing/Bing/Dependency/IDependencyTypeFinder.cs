using Bing.Reflections;

namespace Bing.Dependency
{
    /// <summary>
    /// 依赖注入类型查找器。查找标注了<see cref="DependencyAttribute"/>特性，或者<see cref="ISingletonDependency"/>, <see cref="IScopedDependency"/>, <see cref="ITransientDependency"/>三个接口的服务实现类型
    /// </summary>
    public interface IDependencyTypeFinder : ITypeFinder
    {
    }
}

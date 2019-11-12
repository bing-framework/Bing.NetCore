using Microsoft.Extensions.DependencyInjection;

namespace Bing.Dependency
{
    /// <summary>
    /// 实现此接口的类型将注册为<see cref="ServiceLifetime.Singleton"/>模式
    /// </summary>
    [IgnoreDependency]
    public interface ISingletonDependency
    {
    }
}

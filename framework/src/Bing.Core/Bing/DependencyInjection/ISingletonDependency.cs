using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 实现此接口的类型将注册为 <see cref="ServiceLifetime.Singleton"/> 模式
/// </summary>
/// <remarks>生命周期为单例</remarks>
[IgnoreDependency]
public interface ISingletonDependency
{
}

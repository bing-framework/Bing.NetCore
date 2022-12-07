using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 实现此接口的类型将自动注册为 <see cref="ServiceLifetime.Transient"/> 模式
/// </summary>
/// <remarks>生命周期为每次创建一个新实例</remarks>
[IgnoreDependency]
public interface ITransientDependency
{
}

using Microsoft.Extensions.DependencyInjection;

namespace Bing.DependencyInjection;

/// <summary>
/// 将框架作用域工厂抽象委托给底层 <see cref="IServiceScopeFactory"/> 的默认实现。
/// </summary>
[Dependency(ServiceLifetime.Singleton, TryAdd = true)]
public class DefaultServiceScopeFactory : IHybridServiceScopeFactory
{
    /// <summary>
    /// 获取底层服务作用域工厂。
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// 使用底层服务作用域工厂初始化 <see cref="DefaultServiceScopeFactory"/> 的实例。
    /// </summary>
    /// <param name="serviceScopeFactory">实际创建服务作用域的底层工厂。</param>
    public DefaultServiceScopeFactory(IServiceScopeFactory serviceScopeFactory) => ServiceScopeFactory = serviceScopeFactory;

    /// <inheritdoc />
    /// <remarks>直接委托给底层 <see cref="IServiceScopeFactory"/>。</remarks>
    /// <returns>新创建的服务作用域。</returns>
    public IServiceScope CreateScope() => ServiceScopeFactory.CreateScope();
}

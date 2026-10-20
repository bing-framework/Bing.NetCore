using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 依赖模块服务注册测试。
/// </summary>
public class AppServiceAddTest
{
    /// <summary>
    /// 验证依赖模块能够向服务集合添加服务。
    /// </summary>
    [Fact]
    public void Test_Ctor()
    {
        IServiceCollection services = new ServiceCollection();
        var module = new DependencyModule();
        services = module.AddServices(services);
    }

    /// <summary>
    /// 被服务扫描排除的测试服务契约。
    /// </summary>
    [IgnoreDependency]
    private interface IIgnoreService { }

    /// <summary>
    /// 由多个生命周期实现共享的测试服务契约。
    /// </summary>
    private interface ITestService : IIgnoreService { }

    /// <summary>
    /// 瞬时生命周期的服务测试实现。
    /// </summary>
    private class TransientTestService : ITestService, ITransientDependency { }

    /// <summary>
    /// 作用域生命周期的服务测试实现。
    /// </summary>
    private class ScopedTestService : ITestService, IScopedDependency { }

    /// <summary>
    /// 单例生命周期的服务测试实现。
    /// </summary>
    private class SingletonTestService : ITestService, ISingletonDependency { }

    /// <summary>
    /// 泛型服务注册测试契约。
    /// </summary>
    /// <typeparam name="T">服务关联的类型。</typeparam>
    private interface IGenericService<T> { }

    /// <summary>
    /// 作用域生命周期的泛型服务测试实现。
    /// </summary>
    /// <typeparam name="T">服务关联的类型。</typeparam>
    private class GenericService<T> : IGenericService<T>, IScopedDependency { }
}

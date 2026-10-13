using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 忽略依赖扫描特性测试。
/// </summary>
public class IgnoreDependencyAttributeTest
{
    /// <summary>
    /// 验证标记忽略特性的服务不会注册，而其他服务仍会注册。
    /// </summary>
    [Fact]
    public void Test_Ignore()
    {
        IServiceCollection services = new ServiceCollection();
        var module = new DependencyModule();
        services = module.AddServices(services);

        services.ShouldContain(m => m.ServiceType == typeof(ITestService));
        services.ShouldNotContain(m => m.ServiceType == typeof(IIgnoreService));
    }

    /// <summary>
    /// 通过特性排除扫描的测试服务契约。
    /// </summary>
    [IgnoreDependency]
    private interface IIgnoreService { }

    /// <summary>
    /// 继承被排除契约的测试服务接口。
    /// </summary>
    private interface ITestService : IIgnoreService { }

    /// <summary>
    /// 用于验证依赖扫描的瞬时服务实现。
    /// </summary>
    private class TestService : ITestService, ITransientDependency { }
}

using AspectCore.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.DependencyInjection;

/// <summary>
/// 服务集合注册测试。
/// </summary>
public class ServiceCollectionTest
{
    /// <summary>
    /// 验证注册的服务能够从服务提供程序解析。
    /// </summary>
    [Fact]
    public void Test_Add()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<ITestService, TestService1>();

        var provider = services.BuildServiceContextProvider();
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);
    }

    /// <summary>
    /// 服务集合测试使用的服务契约。
    /// </summary>
    private interface ITestService { }

    /// <summary>
    /// 服务集合测试使用的第一种服务实现。
    /// </summary>
    private class TestService1 : ITestService { }

    /// <summary>
    /// 服务集合测试使用的第二种服务实现。
    /// </summary>
    private class TestService2 : ITestService { }
}

using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bing.Tests.DependencyInjection;

public class IgnoreDependencyAttributeTest
{
    [Fact]
    public void Test_Ignore()
    {
        IServiceCollection services = new ServiceCollection();
        var module = new DependencyModule();
        services = module.AddServices(services);

        services.ShouldContain(m => m.ServiceType == typeof(ITestService));
        services.ShouldNotContain(m => m.ServiceType == typeof(IIgnoreService));
    }

    [IgnoreDependency]
    private interface IIgnoreService { }

    private interface ITestService : IIgnoreService { }

    private class TestService : ITestService, ITransientDependency { }
}
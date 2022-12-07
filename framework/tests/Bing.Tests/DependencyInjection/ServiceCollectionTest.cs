using System;
using System.Collections.Generic;
using System.Text;
using AspectCore.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.DependencyInjection;

public class ServiceCollectionTest
{
    [Fact]
    public void Test_Add()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddTransient<ITestService, TestService1>();

        var provider = services.BuildServiceContextProvider();
        var service = provider.GetService<ITestService>();
        Assert.NotNull(service);
    }

    private interface ITestService { }

    private class TestService1 : ITestService { }

    private class TestService2 : ITestService { }
}
using System.Collections.Generic;
using System.Linq;
using AspectCore.Extensions.DependencyInjection;
using Bing.DependencyInjection;
using Bing.Tests.Samples;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Bing.Tests.Helpers;

/// <summary>
/// Ioc测试
/// </summary>
public class IocTest
{
    /// <summary>
    /// 初始化Ioc测试
    /// </summary>
    public IocTest()
    {
        var services = new ServiceCollection();
        services.AddScoped<ISample, Sample>();
        services.AddBing();
        services.AddLogging();
        var serviceProvider = services.BuildServiceContextProvider();
        serviceProvider.UseBing();
    }

    /// <summary>
    /// 测试创建实例
    /// </summary>
    [Fact]
    public void TestCreate()
    {
        var sample = ServiceLocator.Instance.GetService<ISample>();
        Assert.NotNull(sample);
    }

    /// <summary>
    /// 测试集合
    /// </summary>
    [Fact]
    public void TestCollection()
    {
        var samples = ServiceLocator.Instance.GetService<IEnumerable<ISample>>();
        Assert.NotNull(samples);
        Assert.Single(samples);
    }

    /// <summary>
    /// 创建集合
    /// </summary>
    [Fact]
    public void TestCreateList()
    {
        var samples = ServiceLocator.Instance.GetServices<ISample>().ToList();
        Assert.NotNull(samples);
        Assert.Single(samples);
    }

    /// <summary>
    /// 创建集合
    /// </summary>
    [Fact]
    public void TestCreateList_2()
    {
        var samples = ((IEnumerable<ISample>)ServiceLocator.Instance.GetServices(typeof(ISample))).ToList();
        Assert.NotNull(samples);
        Assert.Single(samples);
    }
}
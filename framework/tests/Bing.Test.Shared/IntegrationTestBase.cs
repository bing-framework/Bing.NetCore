using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bing.Test.Shared;

/// <summary>
/// 集成测试基类
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    /// <summary>
    /// 服务提供程序
    /// </summary>
    protected IServiceProvider ServiceProvider { get; private set; }

    /// <summary>
    /// 服务作用域
    /// </summary>
    protected IServiceScope ServiceScope { get; private set; }

    /// <summary>
    /// 初始化一个<see cref="IntegrationTestBase"/>类型的实例
    /// </summary>
    protected IntegrationTestBase() => Initialize();

    /// <summary>
    /// 初始化
    /// </summary>
    protected void Initialize()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(x => x.AddProvider(NullLoggerProvider.Instance));
        ConfigureServices(serviceCollection);
        if (ServiceProvider != null)
            return;
        ServiceProvider = serviceCollection.BuildServiceProvider();
        ServiceScope = ServiceProvider.CreateScope();
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    /// <param name="services">服务集合</param>
    protected abstract void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <param name="disposing">是否释放资源中</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            ServiceScope?.Dispose();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
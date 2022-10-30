﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bing.EventBus.Tests;

/// <summary>
/// 启动配置
/// </summary>
public class Startup
{
    /// <summary>
    /// 配置主机
    /// </summary>
    public void ConfigureHost(IHostBuilder hostBuilder)
    {
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddBing()
            .AddModule<EventBusModule>();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));
    }
}
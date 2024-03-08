using Bing.Serialization.SystemTextJson;
using System;
using AspectCore.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection.Logging;
using Xunit.DependencyInjection;

namespace Bing.AspNetCore.Mvc;

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
        hostBuilder
            .UseServiceContext()
            .ConfigureWebHostDefaults(webHostBuilder =>
            {
                webHostBuilder.UseTestServer()
                    .Configure(ConfigureApp);
            });
    }

    /// <summary>
    /// 进程退出时释放日志实例，用于解决Seq无法写入的问题
    /// </summary>
    private void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddBing()
            .AddModule<BingAspNetCoreMvcTestModule>();
    }

    /// <summary>
    /// 配置请求管道
    /// </summary>
    public void ConfigureApp(IApplicationBuilder app)
    {
        app.UseBing();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));
    }
}

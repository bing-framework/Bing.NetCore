using AspectCore.DynamicProxy.Parameters;
using AspectCore.Extensions.Hosting;
using Bing.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.Logging;

namespace Bing.Aop.AspectCore;

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
        hostBuilder.ConfigureDefaults(null)
            .UseServiceContext();
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        // 日志
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
        services.EnableAop(o =>
        {
            // 启用参数拦截，才会对参数进行验证
            o.EnableParameterAspect();
        });
        services.AddBing();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure()
    {
        
    }
}

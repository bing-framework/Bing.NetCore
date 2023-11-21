using AspectCore.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.DependencyInjection.Logging;

namespace Bing.Localization;

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
        // 本地化
        services.AddJsonLocalization();
        services.AddBing();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure()
    {
        
    }
}

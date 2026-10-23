using Bing.Samples.SqlUnitOfWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Samples.SqlUnitOfWork;

/// <summary>
/// 多数据源工作单元示例入口。
/// </summary>
public static class Program
{
    /// <summary>
    /// 启动 HTTP 宿主。
    /// </summary>
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    /// <summary>
    /// 创建宿主；测试可覆盖存储模式和 Web 监听配置。
    /// </summary>
    public static IHostBuilder CreateHostBuilder(string[] args, string? storageMode = null,
        Action<IWebHostBuilder>? configureWebHost = null,
        Action<IServiceCollection>? configureServices = null) =>
        Host.CreateDefaultBuilder(args)
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .ConfigureAppConfiguration((_, configuration) =>
            {
                if (storageMode != null)
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SqlUow:Storage"] = storageMode
                    });
                }
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                configureWebHost?.Invoke(webBuilder);
                webBuilder.UseStartup<Startup>();
            })
            .ConfigureServices((_, services) => configureServices?.Invoke(services));
}

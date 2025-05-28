using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bing.Samples.Winform;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var serviceProvider = GetServiceProvider();
        using var scope = serviceProvider.CreateScope();
        Application.Run(scope.ServiceProvider.GetService<Form1>());
    }

    /// <summary>
    /// 获取服务提供程序
    /// </summary>
    static IServiceProvider GetServiceProvider()
    {
        var services = new ServiceCollection();
        var configuration = InitConfiguration(services);
        services.AddSingleton(configuration);
        AddServices(services, configuration);
        services.AddBing();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 初始化配置
    /// </summary>
    static IConfiguration InitConfiguration(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection()
            .SetBasePath(AppContext.BaseDirectory)  // 设置为应用程序的执行目录
            .AddJsonFile("appsettings.json", optional: true);
        var configuration = builder.Build();
        services.AddOptions();
        return configuration;
    }

    /// <summary>
    /// 注册服务
    /// </summary>
    static void AddServices(IServiceCollection services, IConfiguration configuration)
    {

    }
}

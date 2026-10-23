using System;
using AspectCore.Extensions.Hosting;
using System.IO;
using Bing.Logging.Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bing.Logging.Tests;

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
            .ConfigureDefaults(null)
            .UseServiceContext()
            .ConfigureAppConfiguration((context, builder) =>
        {
            builder.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"));
        });
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        services.AddBingLogging(x => { });
        services.AddSingleton<ILogContextAccessor, LogContextAccessor>();
        services.AddLogging(loggingBuilder => loggingBuilder.AddXunitOutput());
        services.AddSingleton<InMemoryLogEventSink>();
        services.AddSingleton<global::Serilog.ILogger>(serviceProvider =>
        {
            var configuration = context.Configuration;
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithLogContext()
                .Enrich.WithLogLevel()
                .ReadFrom.Configuration(configuration)
                .ConfigLogLevel(configuration)
                .WriteTo.Sink(serviceProvider.GetRequiredService<InMemoryLogEventSink>())
                .CreateLogger();
        });
        services.AddSingleton<ILoggerProvider>(serviceProvider =>
            new SerilogLoggerProvider(serviceProvider.GetRequiredService<global::Serilog.ILogger>(), dispose: false));
        services.AddBing();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
    }
}

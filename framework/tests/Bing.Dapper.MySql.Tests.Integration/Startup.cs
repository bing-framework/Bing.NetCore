using AspectCore.Extensions.DependencyInjection;
using AspectCore.Extensions.Hosting;
using Bing.Dapper;
using Bing.Dapper.MySql;
using Bing.Data.Sql;
using Bing.Datas.EntityFramework.MySql;
using Bing.DependencyInjection;
using Bing.Tests.UnitOfWorks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;

namespace Bing.Dapper.Tests;

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
            .UseServiceContext()
            .ConfigureAppConfiguration((_, builder) =>
            {
                // 支持通过 appsettings.Development.json 覆盖连接字符串（本地开发，不提交到 Git）
                builder.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                // 支持通过环境变量 ConnectionStrings__DefaultConnection 覆盖（CI/CD 使用）
                builder.AddEnvironmentVariables();
            });
    }

    /// <summary>
    /// 配置服务
    /// </summary>
    public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
    {
        var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
        services.AddMySqlUnitOfWork<ITestUnitOfWork, MySqlUnitOfWork>(connectionString);
        services.AddMySqlQuery(connectionString);
        services.AddMySqlExecutor(connectionString);
        services.AddEntityMetadata<MySqlUnitOfWork>();
        // 日志
        services.AddLogging(logBuilder => logBuilder.AddXunitOutput());
        services.EnableAop();
        services.AddBing();
    }

    /// <summary>
    /// 配置日志提供程序
    /// </summary>
    public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
    {
        //// 添加单元测试日志提供程序，并配置日志过滤
        //loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));

        //var listener = new ActivityListener();
        //listener.ShouldListenTo += _ => true;
        //listener.Sample += delegate { return ActivitySamplingResult.AllDataAndRecorded; };

        //ActivitySource.AddActivityListener(listener);
    }
}

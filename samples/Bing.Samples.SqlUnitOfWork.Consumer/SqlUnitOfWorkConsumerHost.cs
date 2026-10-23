using Bing.Data.Enums;
using Bing.Events.Cap;
using Bing.Samples.SqlUnitOfWork.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bing.Samples.SqlUnitOfWork.Consumer;

/// <summary>
/// 创建仅依赖 CAP runtime 的独立消费宿主。
/// </summary>
public static class SqlUnitOfWorkConsumerHost
{
    /// <summary>
    /// 创建包含 orders 与 reporting 消费 runtime 的宿主构建器。
    /// </summary>
    /// <param name="args">进程命令行参数。</param>
    /// <returns>独立 Consumer 宿主构建器。</returns>
    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        var ordersConnectionString = Environment.GetEnvironmentVariable("BING_SQLUOW_MYSQL_ORDERS") ??
            "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_orders;Pooling=true";
        var reportingConnectionString = Environment.GetEnvironmentVariable("BING_SQLUOW_MYSQL_REPORTING") ??
            "Server=127.0.0.1;Port=13307;User ID=cap_app;Password=cap_local_only;Database=bing_sqluow_reporting;Pooling=true";
        var rabbitConnectionString = Environment.GetEnvironmentVariable("BING_SQLUOW_CAP_RABBITMQ") ??
            "amqp://cap_local:cap_local_only@127.0.0.1:5673/sqluow";

        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((_, services) =>
            {
                services.AddHostedService<ConsumerMySqlSchemaInitializer>();
                services.AddCapUnitOfWorkMessagePublisher(cap => cap
                    .AddDataSource("orders", DatabaseType.MySql, ordersConnectionString,
                        (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(
                            options, connectionString, rabbitConnectionString, "sqluow-orders-consumer"),
                        sourceServices =>
                        {
                            sourceServices.AddSingleton(new CapConsumerSourceOptions("orders", ordersConnectionString,
                                "sqluow-orders-consumer"));
                            sourceServices.AddScoped<CapConsumerProcessingService>();
                            sourceServices.AddScoped<OrdersCapConsumer>();
                        }, publisherOnly: false)
                    .AddDataSource("reporting", DatabaseType.MySql, reportingConnectionString,
                        (options, connectionString) => SqlUnitOfWorkCapRuntimeConfiguration.Configure(
                            options, connectionString, rabbitConnectionString, "sqluow-reporting-consumer"),
                        sourceServices =>
                        {
                            sourceServices.AddSingleton(new CapConsumerSourceOptions("reporting", reportingConnectionString,
                                "sqluow-reporting-consumer"));
                            sourceServices.AddScoped<CapConsumerProcessingService>();
                            sourceServices.AddScoped<ReportingCapConsumer>();
                        }, publisherOnly: false));
            });
    }
}

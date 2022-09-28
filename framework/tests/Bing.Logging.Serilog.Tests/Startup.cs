using System;
using System.IO;
using Bing.Logging.Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Xunit.DependencyInjection;
using Xunit.DependencyInjection.Logging;
using serilog = Serilog;

namespace Bing.Logging.Tests
{
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
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            hostBuilder.ConfigureAppConfiguration((context, builder) =>
            {
                builder.AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"));
            });
        }

        /// <summary>
        /// 进程退出时释放日志实例，用于解决Seq无法写入的问题
        /// </summary>
        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            var log = (serilog.Core.Logger)serilog.Log.Logger;
            log.Dispose();
        }

        /// <summary>
        /// 配置服务
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddBingLogging(x => { });
            services.AddSingleton<ILogContextAccessor, LogContextAccessor>();
            services.AddLogging(loggingBuilder =>
            {
                var configuration = services.GetConfiguration();
                serilog.Log.Logger = new serilog.LoggerConfiguration()
                    .Enrich.FromLogContext()
                    .Enrich.WithLogContext()
                    .Enrich.WithLogLevel()
                    .ReadFrom.Configuration(configuration)
                    .ConfigLogLevel(configuration)
                    .CreateLogger();
                loggingBuilder.AddSerilog();
            });
            services.AddBing();
        }

        /// <summary>
        /// 配置日志提供程序
        /// </summary>
        public void Configure(ILoggerFactory loggerFactory, ITestOutputHelperAccessor accessor)
        {
            loggerFactory.AddProvider(new XunitTestOutputLoggerProvider(accessor, (s, logLevel) => logLevel >= LogLevel.Trace));
        }
    }
}
